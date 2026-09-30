import {Fragment, type ReactNode, useEffect, useRef, useState} from "react";
import {Link as RouterLink} from "react-router";
import {useInfiniteQuery} from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Link,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import {statisticsGetCell} from "@/api";
import type {
  StockMovementCellRowDto,
  StockMovementDirection,
  StockMovementDto,
  StockMovementEntryDto,
  StockMovementMetricDto,
} from "@/api/types.gen";
import QueryError from "@/components/QueryError";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {formatDateOnly} from "@/utils/dateOnly";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {STOCK_MOVEMENT_ACTIONS} from "./stockMovementsConstants";
import type {StockMovementsFilterValue} from "./useStockMovementsPivot";

const MOVEMENT_NOUN = {one: "движение", few: "движения", many: "движений"};

export interface StockMovementCellTarget {
  date: string;
  catalogItemIds: string[];
  /** Null when the cell belongs to the total group. */
  itemName: string | null;
  metric: StockMovementMetricDto | null;
  columnLabel: string;
}

interface StockMovementCellDialogProps {
  target: StockMovementCellTarget | null;
  filter: StockMovementsFilterValue;
  timeZoneId: string | undefined;
  onClose: () => void;
}

function actionLabel(action: string) {
  return STOCK_MOVEMENT_ACTIONS.find((a) => a.value === action)?.label ?? action;
}

function isIncoming(direction: StockMovementDirection) {
  return direction === "in" || direction === "transferIn";
}

function SignedQuantity({
  quantity,
  direction,
}: {
  quantity: number;
  direction: StockMovementDirection;
}) {
  const incoming = isIncoming(direction);
  return (
    <Typography
      variant="body2"
      sx={{
        color: incoming ? "success.main" : "error.main",
        fontWeight: 500,
        fontVariantNumeric: "tabular-nums",
      }}
    >
      {incoming ? `+${quantity}` : `−${quantity}`}
    </Typography>
  );
}

function documentLink(m: StockMovementDto): {to: string; label: string} | null {
  if (m.orderId) return {to: `/operations/orders/${m.orderId}`, label: `Заказ №${m.orderNumber}`};
  if (m.receiptId)
    return {to: `/operations/receipts/${m.receiptId}`, label: `Приёмка №${m.receiptNumber}`};
  if (m.writeoffId)
    return {to: `/operations/writeoffs/${m.writeoffId}`, label: `Списание №${m.writeoffNumber}`};
  if (m.stocktakeId)
    return {
      to: `/operations/stocktakes/${m.stocktakeId}`,
      label: `Инвентаризация №${m.stocktakeNumber}`,
    };
  return null;
}

function nettedLabel(m: StockMovementCellRowDto) {
  return m.nettedQuantity >= m.quantity
    ? "взаимозачтено"
    : `взаимозачтено ${m.nettedQuantity} из ${m.quantity}`;
}

function locationLabel(m: StockMovementDto) {
  return [m.warehouseName, m.storagePlaceName, m.storagePlaceNodeName].filter(Boolean).join(" / ");
}

function MovementRow({
  movement,
  showItem,
  depth,
  muted,
  badge,
  branch,
  formatTime,
}: {
  movement: StockMovementDto;
  showItem: boolean;
  depth: number;
  muted?: boolean;
  badge?: ReactNode;
  /** Tree connector drawn before the time, `tree`-style. */
  branch?: "├─" | "└─";
  formatTime: (value: string) => string;
}) {
  const document = documentLink(movement);
  return (
    <TableRow
      sx={{
        ...(depth > 0 && {backgroundColor: "action.hover"}),
        ...(muted && {"& > td": {color: "text.secondary"}, opacity: 0.7}),
      }}
    >
      <TableCell sx={{pl: 2 + depth * 4 - (branch ? 3 : 0), whiteSpace: "nowrap"}}>
        {branch && (
          <Box component="span" sx={{fontFamily: "monospace", color: "text.disabled", mr: 0.5}}>
            {branch}
          </Box>
        )}
        {formatTime(movement.createdAt)}
      </TableCell>
      <TableCell>
        {actionLabel(movement.action)}
        {badge}
      </TableCell>
      <TableCell align="right">
        <SignedQuantity quantity={movement.quantity} direction={movement.direction} />
      </TableCell>
      {showItem && <TableCell>{movement.catalogItem.fullName}</TableCell>}
      <TableCell>
        {locationLabel(movement) || "—"}
        {movement.unitInventoryNumber && (
          <Typography variant="caption" sx={{display: "block", color: "text.secondary"}}>
            Ед. {movement.unitInventoryNumber}
          </Typography>
        )}
      </TableCell>
      <TableCell>
        {document ? (
          // The dialog holds a history entry of its own, so leaving it has to replace that entry.
          <Link component={RouterLink} to={document.to} replace>
            {document.label}
          </Link>
        ) : (
          "—"
        )}
      </TableCell>
      <TableCell>{movement.userName ?? "—"}</TableCell>
    </TableRow>
  );
}

/** A listed movement, followed by the halves of its cancellation pair the cell's metric leaves out. */
function CellMovementRows({
  movement,
  showItem,
  depth,
  formatTime,
}: {
  movement: StockMovementCellRowDto;
  showItem: boolean;
  depth: number;
  formatTime: (value: string) => string;
}) {
  return (
    <>
      <MovementRow
        movement={movement}
        showItem={showItem}
        depth={depth}
        formatTime={formatTime}
        badge={
          movement.nettedQuantity > 0 && (
            <Tooltip title="Отмена и отменённое движение в один день — в таблице взаимозачтены и не учитываются">
              <Chip size="small" variant="outlined" label={nettedLabel(movement)} sx={{ml: 1}} />
            </Tooltip>
          )
        }
      />
      {movement.counterparts.map((counterpart, index) => (
        <MovementRow
          key={counterpart.id}
          movement={counterpart}
          showItem={showItem}
          depth={depth + 1}
          muted
          branch={index === movement.counterparts.length - 1 ? "└─" : "├─"}
          formatTime={formatTime}
          badge={
            <Typography component="span" variant="caption" sx={{ml: 1}}>
              (вне метрики, {nettedLabel(counterpart)})
            </Typography>
          }
        />
      ))}
    </>
  );
}

function GroupRows({
  entry,
  showItem,
  formatTime,
}: {
  entry: StockMovementEntryDto;
  showItem: boolean;
  formatTime: (value: string) => string;
}) {
  const [open, setOpen] = useState(false);
  const itemCount = new Set(entry.movements.map((m) => m.catalogItemId)).size;
  const nettedCount = entry.movements.filter((m) => m.nettedQuantity > 0).length;

  return (
    <>
      <TableRow hover onClick={() => setOpen(!open)} sx={{cursor: "pointer"}}>
        <TableCell sx={{whiteSpace: "nowrap"}}>
          <IconButton size="small" sx={{ml: -1, mr: 0.5}}>
            {open ? (
              <KeyboardArrowDownIcon fontSize="small" />
            ) : (
              <KeyboardArrowRightIcon fontSize="small" />
            )}
          </IconButton>
          {formatTime(entry.firstAt)}–{formatTime(entry.lastAt)}
        </TableCell>
        <TableCell>
          {actionLabel(entry.action)}
          <Chip
            size="small"
            label={pluralCount(entry.movements.length, MOVEMENT_NOUN)}
            sx={{ml: 1}}
          />
          {nettedCount > 0 && (
            <Chip
              size="small"
              variant="outlined"
              label={`взаимозачтено: ${nettedCount}`}
              sx={{ml: 1}}
            />
          )}
        </TableCell>
        <TableCell align="right">
          <SignedQuantity quantity={entry.quantity} direction={entry.direction} />
        </TableCell>
        {showItem && (
          <TableCell>
            {itemCount === 1
              ? entry.movements[0].catalogItem.fullName
              : pluralCount(itemCount, NOUNS.position)}
          </TableCell>
        )}
        <TableCell sx={{color: "text.secondary"}}>—</TableCell>
        <TableCell sx={{color: "text.secondary"}}>Массовая операция</TableCell>
        <TableCell>{entry.userName ?? "—"}</TableCell>
      </TableRow>
      {open &&
        entry.movements.map((m) => (
          <CellMovementRows
            key={m.id}
            movement={m}
            showItem={showItem}
            depth={1}
            formatTime={formatTime}
          />
        ))}
    </>
  );
}

function StockMovementCellContent({
  target,
  filter,
  timeZoneId,
}: {
  target: StockMovementCellTarget;
  filter: StockMovementsFilterValue;
  timeZoneId: string | undefined;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const loadMoreRef = useRef<HTMLTableRowElement>(null);

  const query = useInfiniteQuery({
    queryKey: ["stockMovementCell", filter, target.date, target.catalogItemIds, target.metric],
    initialPageParam: null as string | null,
    queryFn: async ({pageParam, signal}) => {
      const response = await statisticsGetCell({
        body: {
          from: target.date,
          to: target.date,
          warehouseId: filter.warehouseId,
          storagePlaceId: filter.storagePlaceId,
          nodeId: filter.nodeId,
          userId: filter.userId,
          catalogItemIds: target.catalogItemIds,
          metric: target.metric,
          before: pageParam,
        },
        signal,
        throwOnError: true,
      });
      return response.data;
    },
    getNextPageParam: (last) => last.nextBefore ?? undefined,
  });

  const {hasNextPage, isFetchingNextPage, fetchNextPage} = query;
  const pages = query.data?.pages;

  useEffect(() => {
    const sentinel = loadMoreRef.current;
    const root = containerRef.current;
    if (!sentinel || !root || !hasNextPage || isFetchingNextPage) return;
    if (typeof IntersectionObserver === "undefined") return;

    const observer = new IntersectionObserver(
      (observed) => observed[0]?.isIntersecting && fetchNextPage(),
      {root, rootMargin: "300px"},
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, pages]);

  // The day was cut in this zone, so times read in it too — a browser elsewhere would show a movement
  // at 01:30 under a day it does not belong to.
  const formatTime = (value: string) =>
    new Date(value).toLocaleTimeString("ru-RU", {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      timeZone: timeZoneId,
    });

  const showItem = target.catalogItemIds.length > 1;
  const columnCount = showItem ? 7 : 6;
  const entries = pages?.flatMap((page) => page.entries) ?? [];
  const totalCount = pages?.[0]?.totalCount;
  const hasNetted = entries.some((e) => e.movements.some((m) => m.nettedQuantity > 0));

  if (query.error) return <QueryError />;

  return (
    <>
      {totalCount != null && (
        <Typography variant="body2" sx={{mb: 1, color: "text.secondary"}}>
          Всего {pluralCount(totalCount, MOVEMENT_NOUN)}
        </Typography>
      )}
      {hasNetted && (
        <Alert severity="info" sx={{mb: 1}}>
          Взаимозачтённое количество в число ячейки не входит: отмена в тот же день гасит отменённое
          движение. Если вторая половина пары не попадает в метрику ячейки, она показана серым под
          строкой.
        </Alert>
      )}
      <TableContainer ref={containerRef} sx={{flex: 1, minHeight: 0}}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Время</TableCell>
              <TableCell>Действие</TableCell>
              <TableCell align="right">Кол-во</TableCell>
              {showItem && <TableCell>Позиция</TableCell>}
              <TableCell>Место</TableCell>
              <TableCell>Документ</TableCell>
              <TableCell>Пользователь</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {query.isLoading ? (
              <TableRowLoader colSpan={columnCount} />
            ) : entries.length === 0 ? (
              <TableRowEmpty colSpan={columnCount} message="Движений нет" />
            ) : (
              entries.map((entry) => (
                <Fragment key={entry.movements[0].id}>
                  {entry.isGroup ? (
                    <GroupRows entry={entry} showItem={showItem} formatTime={formatTime} />
                  ) : (
                    <CellMovementRows
                      movement={entry.movements[0]}
                      showItem={showItem}
                      depth={0}
                      formatTime={formatTime}
                    />
                  )}
                </Fragment>
              ))
            )}
            {hasNextPage && (
              <TableRow ref={loadMoreRef}>
                <TableCell colSpan={columnCount} align="center">
                  {isFetchingNextPage ? (
                    <CircularProgress size={20} />
                  ) : (
                    <Button size="small" onClick={() => fetchNextPage()}>
                      Загрузить ещё
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </>
  );
}

function StockMovementCellDialog({
  target,
  filter,
  timeZoneId,
  onClose,
}: StockMovementCellDialogProps) {
  const [shownTarget, releaseShownTarget] = useRetainedValue(target);

  useBackClosable(target !== null, onClose);

  return (
    <Dialog
      open={target !== null}
      onClose={onClose}
      maxWidth="lg"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownTarget},
        paper: {sx: {pointerEvents: target ? undefined : "none", height: "80vh"}},
      }}
    >
      {shownTarget && (
        <>
          <DialogTitle sx={{pr: 6}}>
            {formatDateOnly(shownTarget.date)} · {shownTarget.columnLabel}
            <Typography variant="body2" sx={{color: "text.secondary"}}>
              {shownTarget.itemName ?? "Все выбранные позиции"}
            </Typography>
            <IconButton onClick={onClose} sx={{position: "absolute", right: 8, top: 8}}>
              <CloseIcon />
            </IconButton>
          </DialogTitle>
          <DialogContent sx={{display: "flex", flexDirection: "column", overflow: "hidden"}}>
            <StockMovementCellContent
              target={shownTarget}
              filter={filter}
              timeZoneId={timeZoneId}
            />
          </DialogContent>
        </>
      )}
    </Dialog>
  );
}

export default StockMovementCellDialog;
