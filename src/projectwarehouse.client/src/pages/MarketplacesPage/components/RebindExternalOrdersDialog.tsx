import {useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  LinearProgress,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesGetAccountsShortOptions,
  marketplacesRebindExternalOrdersMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  RebindExternalOrdersItemDto,
  RebindExternalOrdersRequest,
  RebindExternalOrdersResponse,
} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import MarketplaceAccountPicker from "@/components/marketplace/MarketplaceAccountPicker";
import TableRowEmpty from "@/components/TableRowEmpty";
import {extractErrorMessage} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {parseDateOnly} from "@/utils/dateOnly";

interface RebindExternalOrdersDialogProps {
  open: boolean;
  onClose: () => void;
  /** Fixes the run to one account and hides the account picker. */
  accountId?: string;
}

function RebindExternalOrdersDialog({open, onClose, accountId}: RebindExternalOrdersDialogProps) {
  const queryClient = useQueryClient();
  const [shown, releaseShown] = useRetainedValue(open || null);
  // Kept outside the mutation: a second mutate() clears `data` while pending, which would drop the
  // dry-run preview the moment «Применить» is pressed.
  const [outcome, setOutcome] = useState<{
    request: RebindExternalOrdersRequest;
    response: RebindExternalOrdersResponse;
  } | null>(null);

  const mutation = useMutation({
    ...marketplacesRebindExternalOrdersMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data, variables) => {
      setOutcome({request: variables.body, response: data});
      if (variables.body.dryRun) return;
      for (const id of ["ordersGetAll", "ordersGetById"])
        void queryClient.invalidateQueries({queryKey: byOperation(id)});
    },
  });

  const requestClose = () => {
    if (!mutation.isPending) onClose();
  };

  useBackClosable(open && !mutation.isPending, onClose);

  return (
    <Dialog
      open={open}
      onClose={requestClose}
      fullWidth
      maxWidth={outcome ? "md" : "sm"}
      slotProps={{
        transition: {
          onExited: () => {
            releaseShown();
            setOutcome(null);
            mutation.reset();
          },
        },
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      <DialogTitle>Перепривязка внешних заказов</DialogTitle>
      <LinearProgress sx={{visibility: mutation.isPending ? "visible" : "hidden"}} />
      {shown && (
        <RebindExternalOrdersDialogContent
          accountId={accountId}
          result={outcome?.response}
          lastRequest={outcome?.request}
          error={mutation.error ? extractErrorMessage(mutation.error) : null}
          isPending={mutation.isPending}
          onRun={(body) => mutation.mutate({body})}
          onBack={() => {
            setOutcome(null);
            mutation.reset();
          }}
          onClose={requestClose}
        />
      )}
    </Dialog>
  );
}

interface RebindExternalOrdersDialogContentProps {
  accountId?: string;
  result: RebindExternalOrdersResponse | undefined;
  lastRequest: RebindExternalOrdersRequest | undefined;
  error: string | null;
  isPending: boolean;
  onRun: (body: RebindExternalOrdersRequest) => void;
  onBack: () => void;
  onClose: () => void;
}

function RebindExternalOrdersDialogContent({
  accountId,
  result,
  lastRequest,
  error,
  isPending,
  onRun,
  onBack,
  onClose,
}: RebindExternalOrdersDialogContentProps) {
  const [selected, setSelected] = useState<Set<string>>(
    () => new Set(accountId ? [accountId] : []),
  );
  const [since, setSince] = useState("");
  const [dryRun, setDryRun] = useState(true);

  const {data: accounts, isLoading} = useQuery({
    ...marketplacesGetAccountsShortOptions(),
    enabled: !accountId,
  });

  if (result && lastRequest) {
    return (
      <>
        <DialogContent dividers>
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}
            {lastRequest.dryRun ? (
              <Alert severity="info">Пробный прогон — изменения не сохранены.</Alert>
            ) : (
              <Alert severity="success">Изменения сохранены.</Alert>
            )}
            <Typography variant="body2">
              {lastRequest.dryRun ? "Будет перепривязано" : "Перепривязано"}:{" "}
              {pluralCount(result.orders, NOUNS.order)}, строк — {result.lines}.
            </Typography>
            <ChangesTable items={result.items} showAccount={lastRequest.accountIds.length > 1} />
          </Stack>
        </DialogContent>
        <DialogActions>
          {lastRequest.dryRun ? (
            <>
              <Button onClick={onBack} disabled={isPending}>
                Назад
              </Button>
              <Button
                variant="contained"
                disabled={isPending || result.lines === 0}
                startIcon={isPending ? <CircularProgress size={14} /> : undefined}
                onClick={() => onRun({...lastRequest, dryRun: false})}
              >
                Применить
              </Button>
            </>
          ) : (
            <Button onClick={onClose}>Закрыть</Button>
          )}
        </DialogActions>
      </>
    );
  }

  return (
    <>
      <DialogContent dividers>
        <Stack spacing={2}>
          {error && <Alert severity="error">{error}</Alert>}
          <Typography variant="body2" color="text.secondary">
            Позиции внешних заказов с выбранной даты переносятся на текущую привязку их карточек —
            вместе с коробкой, возвратами и начислениями. Аналитика за этот период пересчитается по
            новой привязке. Заказы, которые собирались на складе, не затрагиваются.
          </Typography>
          <TextField
            type="date"
            label="С даты"
            value={since}
            onChange={(e) => setSince(e.target.value)}
            slotProps={{inputLabel: {shrink: true}}}
            disabled={isPending}
            fullWidth
          />
          <FormControlLabel
            control={<Switch checked={dryRun} onChange={(e) => setDryRun(e.target.checked)} />}
            label="Пробный прогон (без сохранения)"
          />
          {!accountId &&
            (isLoading ? (
              <CircularProgress size={24} />
            ) : (accounts?.length ?? 0) === 0 ? (
              <Typography variant="body2" color="text.secondary">
                Нет магазинов.
              </Typography>
            ) : (
              <MarketplaceAccountPicker
                accounts={accounts ?? []}
                selected={selected}
                onChange={setSelected}
              />
            ))}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          disabled={selected.size === 0 || !since || isPending}
          startIcon={isPending ? <CircularProgress size={14} /> : undefined}
          onClick={() =>
            onRun({
              accountIds: [...selected],
              since: parseDateOnly(since).toISOString(),
              dryRun,
            })
          }
        >
          {dryRun ? "Проверить" : "Перепривязать"}
        </Button>
      </DialogActions>
    </>
  );
}

function ChangesTable({
  items,
  showAccount,
}: {
  items: RebindExternalOrdersItemDto[];
  showAccount: boolean;
}) {
  const colSpan = showAccount ? 5 : 4;

  return (
    <TableContainer sx={{maxHeight: 420}}>
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            {showAccount && <TableCell>Магазин</TableCell>}
            <TableCell>Карточка</TableCell>
            <TableCell>Было</TableCell>
            <TableCell>Стало</TableCell>
            <TableCell align="right">Заказов</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.length === 0 ? (
            <TableRowEmpty colSpan={colSpan} message="Изменений нет" />
          ) : (
            items.map((item) => (
              <TableRow key={`${item.cardId}:${item.oldCatalogItemId ?? ""}`}>
                {showAccount && <TableCell>{item.accountName}</TableCell>}
                <TableCell>
                  <Typography variant="body2" sx={{fontFamily: "monospace"}}>
                    {item.offerId}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {item.cardName}
                  </Typography>
                </TableCell>
                <TableCell>
                  <CatalogItemCell
                    fullName={item.oldCatalogItemFullName}
                    article={item.oldCatalogItemArticle}
                  />
                </TableCell>
                <TableCell>
                  <CatalogItemCell
                    fullName={item.newCatalogItemFullName}
                    article={item.newCatalogItemArticle}
                  />
                </TableCell>
                <TableCell align="right">{item.orders}</TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

function CatalogItemCell({fullName, article}: {fullName?: string | null; article?: string | null}) {
  if (!fullName)
    return (
      <Typography variant="body2" color="text.secondary">
        не сопоставлено
      </Typography>
    );

  return (
    <Typography variant="body2">
      {fullName}
      {article && (
        <Typography component="span" variant="body2" color="text.secondary">
          {` (${article})`}
        </Typography>
      )}
    </Typography>
  );
}

export default RebindExternalOrdersDialog;
