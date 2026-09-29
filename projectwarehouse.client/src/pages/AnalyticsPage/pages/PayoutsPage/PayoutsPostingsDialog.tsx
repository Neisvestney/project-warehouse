import {useState} from "react";
import {
  Alert,
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Link,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {analyticsGetPayoutsPostingsOptions} from "@/api/@tanstack/react-query.gen";
import type {PayoutsBucket, PayoutsDto, PayoutsPostingDto} from "@/api/types.gen";
import {encodePostingsTarget, type PostingsTarget} from "./payoutsPostingsTarget";
import {formatMoney} from "@/components/analytics/analyticsFormat";
import ChannelChip from "@/components/analytics/ChannelChip";
import DataTableContainer from "@/components/DataTableContainer";
import LinkTableRow from "@/components/LinkTableRow";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {formatOrderNumber} from "@/components/orders/orderUtils";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatPostingNumber} from "@/utils/postingNumberUtils";

function bucketTitle(bucket: PayoutsBucket, settings: PayoutsDto["settings"]): string {
  switch (bucket) {
    case "inTransit":
      return "В пути";
    case "inTransitOverdue":
      return `В пути дольше ${settings.payoutOverdueDays} дн.`;
    case "deliveredNotAccrued":
      return "Доставлено, не начислено";
    case "notAccruedByMarketplace":
      return "Площадка не начислила";
  }
}

const formatDate = (value: string) => new Date(value).toLocaleDateString("ru-RU");

interface PayoutsPostingsDialogProps {
  target: PostingsTarget | null;
  onClose: () => void;
  data: PayoutsDto;
  /** Shop selection of the page, used when the target names no shop. */
  channelsQuery: {IncludeMarketplaces?: boolean; MarketplaceAccountIds?: string[]};
}

function PayoutsPostingsDialog({target, onClose, data, channelsQuery}: PayoutsPostingsDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [shown, release] = useRetainedValue(target);

  return (
    <Dialog
      open={!!target}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      fullScreen={isMobile}
      slotProps={{
        transition: {onExited: release},
        paper: {sx: {pointerEvents: target ? undefined : "none", height: {sm: "85vh"}}},
      }}
    >
      {shown && (
        <PayoutsPostingsDialogContent
          key={encodePostingsTarget(shown)}
          target={shown}
          onClose={onClose}
          data={data}
          channelsQuery={channelsQuery}
          isMobile={isMobile}
        />
      )}
    </Dialog>
  );
}

/**
 * Mounted per opening and keyed by the target, so paging starts at the first page every time — also when another
 * bucket is opened while the previous one is still sliding out.
 */
function PayoutsPostingsDialogContent({
  target,
  onClose,
  data,
  channelsQuery,
  isMobile,
}: Omit<PayoutsPostingsDialogProps, "target"> & {target: PostingsTarget; isMobile: boolean}) {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const {
    data: postings,
    error,
    isError,
    isFetching,
    isLoading,
  } = useQuery({
    ...analyticsGetPayoutsPostingsOptions({
      query: {
        Bucket: target.bucket,
        CurrencyCode: target.currencyCode,
        ...(target.accountId
          ? {IncludeMarketplaces: true, MarketplaceAccountIds: [target.accountId]}
          : {
              IncludeMarketplaces: channelsQuery.IncludeMarketplaces,
              MarketplaceAccountIds: channelsQuery.MarketplaceAccountIds,
            }),
        Page: page,
        PageSize: pageSize,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const account = target.accountId
    ? data.rows.find((r) => r.marketplaceAccountId === target.accountId)
    : undefined;
  const subtitle = [
    account ? account.name : "все выбранные магазины",
    target.currencyCode,
    "сумма — по цене продажи, без доли выплаты",
  ].join(" · ");
  const items = postings?.items ?? [];
  const showShop = !target.accountId;

  return (
    <>
      <DialogTitle sx={{display: "flex", alignItems: "center", gap: 1, pr: 1}}>
        <Box sx={{flexGrow: 1, minWidth: 0}}>
          {bucketTitle(target.bucket, data.settings)}
          <Typography variant="body2" color="text.secondary">
            {subtitle}
          </Typography>
        </Box>
        <IconButton onClick={onClose} aria-label="Закрыть">
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <DialogContent dividers sx={{p: isMobile ? 1 : 2}}>
        {isError && (
          <Alert severity="error" sx={{mb: 2}}>
            {extractErrorMessage(error)}
          </Alert>
        )}
        <DataTableContainer
          variant="outlined"
          isFetching={isFetching}
          count={postings?.total ?? 0}
          page={page}
          onPageChange={setPage}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(size) => {
            setPageSize(size);
            setPage(1);
          }}
          disableFloatingPagination
        >
          {isMobile ? (
            <Stack spacing={1} sx={{p: 1}}>
              {items.map((p) => (
                <PostingCard
                  key={p.orderId}
                  posting={p}
                  currencyCode={target.currencyCode}
                  showShop={showShop}
                />
              ))}
              {!isLoading && items.length === 0 && (
                <Typography color="text.secondary" sx={{p: 2, textAlign: "center"}}>
                  Отправлений нет
                </Typography>
              )}
            </Stack>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Отправление</TableCell>
                  <TableCell>Заказ</TableCell>
                  {showShop && <TableCell>Магазин</TableCell>}
                  <TableCell>Отгрузка</TableCell>
                  <TableCell>Доставлено</TableCell>
                  <TableCell align="right">Дней</TableCell>
                  <TableCell align="right">Сумма</TableCell>
                </TableRow>
              </TableHead>
              <TableBody sx={{opacity: isFetching && !isLoading ? 0.5 : 1}}>
                {isLoading ? (
                  <TableRowLoader colSpan={showShop ? 7 : 6} />
                ) : items.length === 0 ? (
                  <TableRowEmpty colSpan={showShop ? 7 : 6} message="Отправлений нет" />
                ) : (
                  items.map((p) => (
                    <LinkTableRow
                      key={p.orderId}
                      to={`/operations/orders/${p.orderId}`}
                      ariaLabel={`Заказ ${formatOrderNumber(p.orderNumber)}`}
                    >
                      <TableCell sx={{whiteSpace: "nowrap"}}>
                        {formatPostingNumber(p.postingNumber)}
                      </TableCell>
                      <TableCell>{formatOrderNumber(p.orderNumber)}</TableCell>
                      {showShop && (
                        <TableCell>
                          {/* raised above the row's link overlay, the chip is a link of its own */}
                          <Box sx={{position: "relative", zIndex: 1, display: "inline-flex"}}>
                            <ChannelChip row={shopRef(p)} />
                          </Box>
                        </TableCell>
                      )}
                      <TableCell>{formatDate(p.effectiveDate)}</TableCell>
                      <TableCell>{p.deliveredAt ? formatDate(p.deliveredAt) : "—"}</TableCell>
                      <TableCell align="right">{p.ageDays}</TableCell>
                      <TableCell align="right" sx={{whiteSpace: "nowrap"}}>
                        {formatMoney(p.amount, target.currencyCode)}
                      </TableCell>
                    </LinkTableRow>
                  ))
                )}
              </TableBody>
            </Table>
          )}
        </DataTableContainer>
      </DialogContent>
    </>
  );
}

function shopRef(p: PayoutsPostingDto) {
  return {
    kind: "marketplace" as const,
    marketplaceAccountId: p.marketplaceAccountId,
    marketplaceType: p.marketplaceType,
    name: p.accountName,
  };
}

function PostingCard({
  posting: p,
  currencyCode,
  showShop,
}: {
  posting: PayoutsPostingDto;
  currencyCode: string;
  showShop: boolean;
}) {
  return (
    <Paper variant="outlined" sx={{p: 1.5}}>
      <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "baseline", gap: 1}}>
        <Typography variant="body2" sx={{whiteSpace: "nowrap"}}>
          {formatPostingNumber(p.postingNumber)}
        </Typography>
        <Link component={RouterLink} to={`/operations/orders/${p.orderId}`} variant="body2">
          {formatOrderNumber(p.orderNumber)}
        </Link>
      </Stack>
      {showShop && (
        <Box sx={{mt: 0.5}}>
          <ChannelChip row={shopRef(p)} />
        </Box>
      )}
      <Typography variant="caption" color="text.secondary" component="div" sx={{mt: 0.5}}>
        {[
          `отгрузка ${formatDate(p.effectiveDate)}`,
          p.deliveredAt && `доставлено ${formatDate(p.deliveredAt)}`,
          `${p.ageDays} дн.`,
          formatMoney(p.amount, currencyCode),
        ]
          .filter(Boolean)
          .join(" · ")}
      </Typography>
    </Paper>
  );
}

export default PayoutsPostingsDialog;
