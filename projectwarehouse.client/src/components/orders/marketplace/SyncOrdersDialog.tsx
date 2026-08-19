import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {
  Alert,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesGetOrderSyncTargetsOptions,
  marketplacesGetSyncRunsByIdsOptions,
  marketplacesSyncOrdersMutation,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {
  MarketplaceOrderSyncTargetDto,
  MarketplaceType,
  SyncOrdersFailedItem,
  SyncOrdersStartedItem,
} from "@/api/types.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import {useEntityWatchMany} from "@/hooks/useEntityWatch";
import {useRealtimeEvent} from "@/hooks/useRealtimeEvent";
import SyncOrdersAccountAccordion from "./SyncOrdersAccountAccordion";
import {MARKETPLACE_LABELS} from "./marketplaceOrderUtils";

/** Fallback poll: runs only while the SSE subscriptions on the picked accounts aren't live. */
const RUNNING_POLL_MS = 2000;

const SHORTCUT_TYPES: MarketplaceType[] = ["ozon", "wildberries"];

interface SyncOrdersDialogProps {
  open: boolean;
  onClose: () => void;
}

function SyncOrdersDialog({open, onClose}: SyncOrdersDialogProps) {
  const queryClient = useQueryClient();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [started, setStarted] = useState<SyncOrdersStartedItem[]>([]);
  const [failed, setFailed] = useState<SyncOrdersFailedItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const wasRunning = useRef(false);

  const {data: targets, isLoading} = useQuery({
    ...marketplacesGetOrderSyncTargetsOptions(),
    enabled: open,
  });

  const isRunningPhase = started.length > 0 || failed.length > 0;
  const runIds = started.map((s) => s.syncRunId);

  // Account ids are known from the targets list before the run starts, so the subscription is in
  // place by the time the first event fires. Started accounts are kept in the set as well —
  // unticking a box mid-run must not drop the subscription that reports its progress.
  const watchedIds = useMemo(
    () => (open ? [...new Set([...selected, ...started.map((s) => s.accountId)])] : []),
    [open, selected, started],
  );

  const refreshRuns = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: byOperation("marketplacesGetSyncRunsByIds")});
  }, [queryClient]);

  const {isWatching} = useEntityWatchMany("marketplaceAccount", watchedIds, refreshRuns);

  const {data: runs} = useQuery({
    ...marketplacesGetSyncRunsByIdsOptions({query: {ids: runIds}}),
    enabled: open && runIds.length > 0,
    refetchInterval: (query) =>
      !isWatching && query.state.data?.some((r) => r.status === "running")
        ? RUNNING_POLL_MS
        : false,
  });

  useRealtimeEvent("marketplaceSyncProgress", (_event, payload) => {
    if (watchedIds.includes(payload.accountId)) refreshRuns();
  });

  useRealtimeEvent("marketplaceSyncFinished", (_event, payload) => {
    if (watchedIds.includes(payload.accountId)) refreshRuns();
  });

  const anyRunning = runs?.some((r) => r.status === "running") ?? runIds.length > 0;

  // invalidate once, on the falling edge — the new orders appear in the list behind the dialog
  useEffect(() => {
    if (anyRunning) wasRunning.current = true;
    else if (wasRunning.current) {
      wasRunning.current = false;
      void queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
    }
  }, [anyRunning, queryClient]);

  const mutation = useMutation({
    ...marketplacesSyncOrdersMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      setStarted(data.items);
      setFailed(data.failedItems);
    },
    onError: (e) => setError(extractErrorMessage(e)),
  });

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function selectAllOfType(type: MarketplaceType) {
    const ids = (targets ?? []).filter((t) => t.type === type).map((t) => t.id);
    setSelected((prev) => new Set([...prev, ...ids]));
  }

  function handleClose() {
    setSelected(new Set());
    setStarted([]);
    setFailed([]);
    setError(null);
    onClose();
  }

  function nameOf(accountId: string) {
    return targets?.find((t) => t.id === accountId)?.name ?? "Магазин";
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Синхронизация заказов</DialogTitle>
      <DialogContent dividers>
        {error && (
          <Alert severity="error" sx={{mb: 2}} onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        {isRunningPhase ? (
          <Stack spacing={1}>
            {started.map((item) => (
              <SyncOrdersAccountAccordion
                key={item.accountId}
                accountName={nameOf(item.accountId)}
                run={runs?.find((r) => r.id === item.syncRunId)}
              />
            ))}
            {failed.map((item) => (
              <SyncOrdersAccountAccordion
                key={item.accountId}
                accountName={item.accountName ?? nameOf(item.accountId)}
                rejection={item.error}
              />
            ))}
          </Stack>
        ) : isLoading ? (
          <CircularProgress size={24} />
        ) : (targets?.length ?? 0) === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Нет магазинов, поддерживающих синхронизацию заказов.
          </Typography>
        ) : (
          <Stack spacing={1}>
            <Stack direction="row" spacing={1}>
              {SHORTCUT_TYPES.filter((type) => targets?.some((t) => t.type === type)).map(
                (type) => (
                  <Button key={type} size="small" onClick={() => selectAllOfType(type)}>
                    Все {MARKETPLACE_LABELS[type]}
                  </Button>
                ),
              )}
            </Stack>
            {targets?.map((target) => (
              <TargetRow
                key={target.id}
                target={target}
                checked={selected.has(target.id)}
                onToggle={() => toggle(target.id)}
              />
            ))}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>{isRunningPhase ? "Закрыть" : "Отмена"}</Button>
        {!isRunningPhase && (
          <Button
            variant="contained"
            disabled={selected.size === 0 || mutation.isPending}
            startIcon={mutation.isPending ? <CircularProgress size={14} /> : undefined}
            onClick={() => mutation.mutate({body: {accountIds: [...selected]}})}
          >
            Синхронизировать
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}

interface TargetRowProps {
  target: MarketplaceOrderSyncTargetDto;
  checked: boolean;
  onToggle: () => void;
}

/** Accounts with gaps stay selectable — a warning explains far more than a missing checkbox. */
function TargetRow({target, checked, onToggle}: TargetRowProps) {
  const warnings = [
    target.mappedWarehouseCount === 0 ? "ни один склад не привязан" : null,
    target.unmappedCardCount > 0 ? `${target.unmappedCardCount} карточек без привязки` : null,
    target.credentialsUnreadable ? "ключ не читается" : null,
    target.isSyncRunning ? "уже идёт синхронизация" : null,
  ].filter((w): w is string => w !== null);

  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <FormControlLabel
        control={<Checkbox size="small" checked={checked} onChange={onToggle} />}
        label={`${target.name} · ${MARKETPLACE_LABELS[target.type]}`}
        sx={{flexGrow: 1}}
      />
      {warnings.length > 0 && (
        <Tooltip title={`Будут пропуски: ${warnings.join(", ")}`}>
          <WarningAmberIcon color="warning" fontSize="small" />
        </Tooltip>
      )}
    </Stack>
  );
}

export default SyncOrdersDialog;
