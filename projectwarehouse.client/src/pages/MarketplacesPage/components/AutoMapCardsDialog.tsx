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
  Typography,
} from "@mui/material";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesAutoMapCardsMutation,
  marketplacesGetAccountsShortOptions,
} from "@/api/@tanstack/react-query.gen";
import type {
  AutoMapCardChangeDto,
  AutoMapCardsRequest,
  AutoMapCardsResponse,
  MarketplaceMappingSource,
} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import MarketplaceAccountPicker from "@/components/marketplace/MarketplaceAccountPicker";
import TableRowEmpty from "@/components/TableRowEmpty";
import {MAPPING_SOURCE_LABELS} from "@/components/marketplace/marketplaceUtils.ts";
import {extractErrorMessage} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

interface AutoMapCardsDialogProps {
  open: boolean;
  onClose: () => void;
  /** Fixes the run to one account and hides the account picker. */
  accountId?: string;
}

function AutoMapCardsDialog({open, onClose, accountId}: AutoMapCardsDialogProps) {
  const queryClient = useQueryClient();
  const [shown, releaseShown] = useRetainedValue(open || null);
  // Kept outside the mutation: a second mutate() clears `data` while pending, which would drop the
  // dry-run preview the moment «Применить» is pressed.
  const [outcome, setOutcome] = useState<{
    request: AutoMapCardsRequest;
    response: AutoMapCardsResponse;
  } | null>(null);

  const mutation = useMutation({
    ...marketplacesAutoMapCardsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data, variables) => {
      setOutcome({request: variables.body, response: data});
      if (variables.body.dryRun) return;
      // not awaited: the mutation would stay pending and swallow «Закрыть» until the refetches land
      for (const id of [
        "marketplacesGetCards",
        "marketplacesGetAccount",
        "marketplacesGetUnmappedCount",
      ])
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
      <DialogTitle>Автосопоставление карточек</DialogTitle>
      <LinearProgress sx={{visibility: mutation.isPending ? "visible" : "hidden"}} />
      {shown && (
        <AutoMapCardsDialogContent
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

interface AutoMapCardsDialogContentProps {
  accountId?: string;
  result: AutoMapCardsResponse | undefined;
  lastRequest: AutoMapCardsRequest | undefined;
  error: string | null;
  isPending: boolean;
  onRun: (body: AutoMapCardsRequest) => void;
  onBack: () => void;
  onClose: () => void;
}

function AutoMapCardsDialogContent({
  accountId,
  result,
  lastRequest,
  error,
  isPending,
  onRun,
  onBack,
  onClose,
}: AutoMapCardsDialogContentProps) {
  const [selected, setSelected] = useState<Set<string>>(
    () => new Set(accountId ? [accountId] : []),
  );
  const [overwriteAuto, setOverwriteAuto] = useState(false);
  const [overwriteManual, setOverwriteManual] = useState(false);
  const [clearUnmatched, setClearUnmatched] = useState(false);
  const [dryRun, setDryRun] = useState(true);

  const {data: accounts, isLoading} = useQuery({
    ...marketplacesGetAccountsShortOptions(),
    enabled: !accountId,
  });

  const canClear = overwriteAuto || overwriteManual;

  const run = (isDryRun: boolean) =>
    onRun({
      accountIds: [...selected],
      overwriteAuto,
      overwriteManual,
      clearUnmatched: canClear && clearUnmatched,
      dryRun: isDryRun,
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
            <ResultSummary result={result} isDryRun={lastRequest.dryRun} />
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
                disabled={isPending || result.items.length === 0}
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
            Несопоставленные карточки обрабатываются всегда. Правила применяются раньше подбора по
            артикулу и штрихкоду.
          </Typography>
          <Stack>
            <FormControlLabel
              control={
                <Switch
                  checked={overwriteAuto}
                  onChange={(e) => setOverwriteAuto(e.target.checked)}
                />
              }
              label="Перебивать автоматически сопоставленные"
            />
            <FormControlLabel
              control={
                <Switch
                  checked={overwriteManual}
                  onChange={(e) => setOverwriteManual(e.target.checked)}
                />
              }
              label="Перебивать сопоставленные вручную"
            />
            <FormControlLabel
              control={
                <Switch
                  checked={canClear && clearUnmatched}
                  disabled={!canClear}
                  onChange={(e) => setClearUnmatched(e.target.checked)}
                />
              }
              label="Сбрасывать привязку, если новое совпадение не найдено"
            />
            <FormControlLabel
              control={<Switch checked={dryRun} onChange={(e) => setDryRun(e.target.checked)} />}
              label="Пробный прогон (без сохранения)"
            />
          </Stack>
          {overwriteManual && clearUnmatched && (
            <Alert severity="warning">
              Вручную обычно привязывают как раз то, что не находится автоматически, — большинство
              ручных привязок будет сброшено. Проверьте результат пробным прогоном.
            </Alert>
          )}
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
          disabled={selected.size === 0 || isPending}
          startIcon={isPending ? <CircularProgress size={14} /> : undefined}
          onClick={() => run(dryRun)}
        >
          {dryRun ? "Проверить" : "Сопоставить"}
        </Button>
      </DialogActions>
    </>
  );
}

function ResultSummary({result, isDryRun}: {result: AutoMapCardsResponse; isDryRun: boolean}) {
  const parts = [
    `Сопоставлено: ${result.mapped}`,
    `перепривязано: ${result.items.length - result.mapped - result.cleared}`,
    `сброшено: ${result.cleared}`,
  ];

  return (
    <Typography variant="body2">
      {parts.join(", ")}. Без привязки {isDryRun ? "останется" : "осталось"}{" "}
      {pluralCount(result.remaining, NOUNS.card)}.
    </Typography>
  );
}

function ChangesTable({items, showAccount}: {items: AutoMapCardChangeDto[]; showAccount: boolean}) {
  const colSpan = showAccount ? 4 : 3;

  return (
    <TableContainer sx={{maxHeight: 420}}>
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            {showAccount && <TableCell>Магазин</TableCell>}
            <TableCell>Карточка</TableCell>
            <TableCell>Было</TableCell>
            <TableCell>Стало</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.length === 0 ? (
            <TableRowEmpty colSpan={colSpan} message="Изменений нет" />
          ) : (
            items.map((item) => (
              <TableRow key={item.cardId}>
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
                  <MappingCell
                    fullName={item.oldCatalogItemFullName}
                    article={item.oldCatalogItemArticle}
                    source={item.oldMappingSource}
                  />
                </TableCell>
                <TableCell>
                  <MappingCell
                    fullName={item.newCatalogItemFullName}
                    article={item.newCatalogItemArticle}
                    source={item.newMappingSource}
                  />
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

interface MappingCellProps {
  fullName?: string | null;
  article?: string | null;
  source?: MarketplaceMappingSource | null;
}

function MappingCell({fullName, article, source}: MappingCellProps) {
  if (!fullName)
    return (
      <Typography variant="body2" color="text.secondary">
        —
      </Typography>
    );

  return (
    <>
      <Typography variant="body2">
        {fullName}
        {article && (
          <Typography component="span" variant="body2" color="text.secondary">
            {` (${article})`}
          </Typography>
        )}
      </Typography>
      {source && (
        <Typography variant="caption" color="text.secondary">
          {MAPPING_SOURCE_LABELS[source]}
        </Typography>
      )}
    </>
  );
}

export default AutoMapCardsDialog;
