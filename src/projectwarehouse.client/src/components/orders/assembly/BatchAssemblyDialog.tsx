import {
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
  type Ref,
} from "react";
import {
  Alert,
  AlertTitle,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CloseIcon from "@mui/icons-material/Close";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import UndoIcon from "@mui/icons-material/Undo";
import {
  type Query,
  useIsFetching,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {ordersGetLastPicks} from "@/api";
import {
  ordersBatchFulfillMutation,
  ordersCheckAssemblyMutation,
  ordersGetAllAssemblyQueryKey,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {
  AddFulfillmentBundleComponentRequest,
  AddFulfillmentRequest,
  AppFieldError,
  AssemblyLastPickDto,
  BatchFulfillFailedItem,
  CatalogItemType,
} from "@/api/types.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useDefaultStorageNode, useDefaultStorageNodeQuery} from "@/hooks/useDefaultStorageNode";
import {useModal} from "@/hooks/useModal";
import {useNodeItemCount} from "@/hooks/useNodeItemCount";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {TodoRegistryProvider, UnfilledCounter} from "./ComponentRow";
import {NO_PICKS, type CompositionPicks, type PicksUpdate} from "./compositionPicks";
import {EMPTY_STATUS, isComplete, type SlotStatus} from "./fulfillmentStatus";
import {useTodoRegistry} from "./todoRegistry";
import {NodeControl, type NodePick} from "./FulfillmentControls";
import {BundleTree} from "./FulfillmentTree";
import {IgnoreStockContext} from "./stockGuard";
import GroupTasksDialog from "./GroupTasksDialog";
import {
  batchGroupKey,
  buildBatchGroups,
  withoutExcludedTasks,
  NO_EXCLUDED_TASKS,
  type BatchGroup,
  type SelectedTaskInfo,
} from "./batchGroups";
import {LAST_PICKS_QUERY_KEY} from "./lastPicks";
import {VariationChain} from "./VariationChain";
import {chainLeaf, type VariantStep} from "./variationOptions";

interface GroupState {
  node: NodePick | null;
  /** Set for a standard group whose cell was picked by hand, over the warehouse default. */
  nodePicked: boolean;
  /** Variant choices for a variation group, one step per nesting level. */
  chain: VariantStep[];
  entries: AddFulfillmentBundleComponentRequest[];
  status: SlotStatus;
  /** Composition choices, kept here so folding the group shut does not lose them. */
  picks: CompositionPicks;
  /** The variants came from the user's previous assembly of the item and are untouched since. */
  hinted: boolean;
}

function emptyGroupState(): GroupState {
  return {
    node: null,
    nodePicked: false,
    chain: [],
    entries: [],
    status: EMPTY_STATUS,
    picks: NO_PICKS,
    hinted: false,
  };
}

function hintedState(hint: AssemblyLastPickDto): Partial<GroupState> {
  const picks: Record<string, {variant: VariantStep}> = {};
  for (const {path, variant} of hint.bundlePicks) picks[path] = {variant};
  return {chain: hint.chain, picks, hinted: true};
}

/** The item stock actually moves against — the variation's leaf, or the group's own item. */
function resolvedItem(
  group: BatchGroup,
  state: GroupState,
): {id: string; type: CatalogItemType} | null {
  if (group.catalogItemType !== "variation") {
    return {id: group.catalogItemId, type: group.catalogItemType};
  }
  const leaf = chainLeaf(state.chain);
  return leaf ? {id: leaf.id, type: leaf.type} : null;
}

/** What still blocks the group, or an empty string when it is ready to go. */
function groupBlocker(group: BatchGroup, state: GroupState): string {
  const resolved = resolvedItem(group, state);
  if (!resolved) return "нужен вариант";
  if (resolved.type === "standard") return state.node ? "" : "нужна ячейка";
  return isComplete(state.status) ? "" : "задать состав";
}

const FULFILLMENTS = {one: "фулфилмент", few: "фулфилмента", many: "фулфилментов"};

/** What actually landed in the database, which under all-or-nothing is nothing at all. */
function describeOutcome(
  sent: number,
  failed: number,
  allowPartialSuccess: boolean,
): {message: string; variant: "success" | "warning" | "error"} {
  if (failed === 0) {
    return {message: `Собрано ${pluralCount(sent, FULFILLMENTS)}`, variant: "success"};
  }
  if (!allowPartialSuccess) {
    return {
      message: `Партия откачена целиком: ${pluralCount(failed, FULFILLMENTS)} из ${sent} с ошибкой`,
      variant: "error",
    };
  }
  return {
    message: `Собрано ${pluralCount(sent - failed, FULFILLMENTS)} из ${sent}, с ошибкой — ${failed}`,
    variant: "warning",
  };
}

type BatchAssemblyMode = "assemble" | "check";

interface BatchAssemblyDialogProps {
  open: boolean;
  onClose: () => void;
  /** `check` sends the same picks to the stock check instead of recording them. */
  mode?: BatchAssemblyMode;
  /** Fires before `onClose` when the whole batch went through; a cancel calls only `onClose`. */
  onAssembled?: () => void;
  /** After a partial success: the tasks that got every remaining component, the dialog stays open. */
  onPartlyAssembled?: (taskIds: ReadonlySet<string>) => void;
  selectedTasks: SelectedTaskInfo[];
  /** Shown above the groups — what the caller left out of the batch and why. */
  notice?: string;
}

function BatchAssemblyDialog({
  open,
  onClose,
  mode = "assemble",
  onAssembled,
  onPartlyAssembled,
  selectedTasks,
  notice,
}: BatchAssemblyDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  // The content is unmounted only after the exit animation; that is what resets the per-group picks.
  const [shownTasks, releaseShownTasks] = useRetainedValue(open ? selectedTasks : null);

  // The confirm and the pending guard live with the content, which owns the mutation.
  const requestCloseRef = useRef<(() => void) | null>(null);

  // A misclick on the backdrop or Back would throw away every pick, so only Esc and Cancel close.
  // A check records nothing, so Back may close it like any other dialog.
  useBackClosable(open, onClose, {blockBack: mode === "assemble"});

  return (
    <Dialog
      open={open}
      onClose={(_event, reason) => {
        if (reason === "escapeKeyDown") requestCloseRef.current?.();
      }}
      maxWidth="lg"
      fullWidth
      fullScreen={isMobile}
      slotProps={{
        transition: {onExited: releaseShownTasks},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownTasks && (
        <BatchAssemblyContent
          onClose={onClose}
          mode={mode}
          onAssembled={onAssembled}
          onPartlyAssembled={onPartlyAssembled}
          requestCloseRef={requestCloseRef}
          selectedTasks={shownTasks}
          notice={notice}
          isMobile={isMobile}
        />
      )}
    </Dialog>
  );
}

interface BatchAssemblyContentProps extends Omit<BatchAssemblyDialogProps, "open" | "mode"> {
  mode: BatchAssemblyMode;
  requestCloseRef: Ref<(() => void) | null>;
  isMobile: boolean;
}

/** A check result belongs to the picks it was made for; any change in them makes it stale. */
interface CheckResult {
  signature: string;
  shortages: AppFieldError[];
}

function BatchAssemblyContent({
  onClose,
  mode,
  onAssembled,
  onPartlyAssembled,
  requestCloseRef,
  selectedTasks,
  notice,
  isMobile,
}: BatchAssemblyContentProps) {
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const {showConfirm} = useModal();
  const groups = useMemo(() => buildBatchGroups(selectedTasks), [selectedTasks]);
  const {registry, scrollToFirst} = useTodoRegistry();
  const checking = mode === "check";

  const [groupStates, setGroupStates] = useState<Map<string, GroupState>>(new Map());
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [excludedKeys, setExcludedKeys] = useState<ReadonlySet<string>>(new Set());
  const [excludedTasks, setExcludedTasks] = useState<ReadonlyMap<string, ReadonlySet<string>>>(
    new Map(),
  );
  const [tasksDialogKey, setTasksDialogKey] = useState<string | null>(null);
  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);
  const [shortageErrors, setShortageErrors] = useState<AppFieldError[]>([]);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [allowPartialSuccess, setAllowPartialSuccess] = useState(false);
  const [ignoreStock, setIgnoreStock] = useState(false);
  const [checkResult, setCheckResult] = useState<CheckResult | null>(null);
  const submittingRef = useRef(false);
  const confirmingCloseRef = useRef(false);
  const [hintsApplied, setHintsApplied] = useState(false);

  const getState = useCallback(
    (key: string) => groupStates.get(key) ?? emptyGroupState(),
    [groupStates],
  );

  const patchState = useCallback((key: string, patch: Partial<GroupState>) => {
    setGroupStates((prev) =>
      new Map(prev).set(key, {...(prev.get(key) ?? emptyGroupState()), ...patch}),
    );
  }, []);

  const resetGroup = useCallback((key: string) => {
    setGroupStates((prev) => new Map(prev).set(key, emptyGroupState()));
  }, []);

  // Standard groups pick nothing but a cell, and cells always start from the warehouse default.
  const hintItems = groups
    .filter((g) => g.catalogItemType !== "standard")
    .map((g) => ({catalogItemId: g.catalogItemId, warehouseId: g.warehouseId}));
  const hintsQuery = useQuery({
    queryKey: [...LAST_PICKS_QUERY_KEY, hintItems],
    queryFn: async ({signal}) => {
      const response = await ordersGetLastPicks({
        body: {items: hintItems},
        signal,
        throwOnError: true,
      });
      return response.data;
    },
    enabled: hintItems.length > 0,
    staleTime: Infinity,
    // a missing hint only means picking by hand, so a failure is neither retried nor reported
    retry: false,
    meta: {suppressGlobalError: true},
  });
  const hints = hintsQuery.data;
  const fetching = useSettledFetching();
  const awaitingHints = hintItems.length > 0 && !hintsApplied && !hintsQuery.isError;

  /** Not ready yet, but only because something is still on its way: the hints, or a hinted tree's levels. */
  const groupLoading = (group: BatchGroup) => {
    const state = getState(group.key);
    if (groupBlocker(group, state) === "") return false;
    if (awaitingHints && group.catalogItemType !== "standard") return true;
    if (!state.hinted || resolvedItem(group, state)?.type !== "bundle") return false;
    // a tree that has not reported once has not even started fetching
    return fetching || state.status.total === 0;
  };

  // Applied once, and only to groups the user has not started on while the hints were loading.
  // Done in render, so no frame shows the groups as arrived-but-empty.
  if (hints && !hintsApplied) {
    setHintsApplied(true);
    setGroupStates((prev) => {
      const next = new Map(prev);
      for (const hint of hints) {
        const key = batchGroupKey(hint.catalogItemId, hint.warehouseId);
        const state = prev.get(key) ?? emptyGroupState();
        if (state.chain.length > 0 || Object.keys(state.picks).length > 0) continue;
        next.set(key, {...state, ...hintedState(hint)});
      }
      return next;
    });
  }

  // The state of an excluded group is kept, so bringing it back restores the composition with it.
  const toggleExcluded = useCallback((key: string) => {
    setExcludedKeys((prev) => {
      const next = new Set(prev);
      if (!next.delete(key)) next.add(key);
      return next;
    });
    setExpandedKey((prev) => (prev === key ? null : prev));
  }, []);

  const toggleTaskExcluded = useCallback((groupKey: string, taskId: string) => {
    setExcludedTasks((prev) => {
      const next = new Map(prev);
      const tasks = new Set(prev.get(groupKey) ?? NO_EXCLUDED_TASKS);
      if (!tasks.delete(taskId)) tasks.add(taskId);
      next.set(groupKey, tasks);
      return next;
    });
  }, []);

  const patchPicks = useCallback((key: string, update: PicksUpdate) => {
    setGroupStates((prev) => {
      const state = prev.get(key) ?? emptyGroupState();
      return new Map(prev).set(key, {...state, picks: update(state.picks), hinted: false});
    });
  }, []);

  const checkMutation = useMutation({
    ...ordersCheckAssemblyMutation(),
    meta: {suppressGlobalError: true},
    onError: (error) => setSubmitError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  const mutation = useMutation({
    ...ordersBatchFulfillMutation(),
    meta: {suppressGlobalError: true},
    // Awaited: the mutation stays pending until the refetch lands, so the button cannot be pressed
    // again against stale groups. Invalidated on partial success too, so the refetched tasks shrink
    // the groups to what is still missing and a retry cannot re-send what already went through.
    onSuccess: async (data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
        queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()}),
        queryClient.invalidateQueries({queryKey: LAST_PICKS_QUERY_KEY}),
      ]);
      setFailedItems(data.failedItems);
      setShortageErrors(data.insufficientInventoryErrors);
      const outcome = describeOutcome(
        variables.body.items.length,
        data.failedItems.length,
        variables.body.allowPartialSuccess,
      );
      enqueueSnackbar(outcome.message, {variant: outcome.variant});
      if (data.failedItems.length === 0) {
        onAssembled?.();
        onClose();
      }
    },
    onError: (error) => setSubmitError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });
  const isPending = mutation.isPending || checkMutation.isPending;

  async function requestClose() {
    // submittingRef flips synchronously, before the re-render that would expose isPending
    if (submittingRef.current || isPending || confirmingCloseRef.current) return;
    if (checking) {
      onClose();
      return;
    }
    confirmingCloseRef.current = true;
    const ok = await showConfirm({
      title: "Прервать сборку?",
      message: "Выбранные ячейки и составы не сохранятся.",
      confirmText: "Закрыть",
      severity: "warning",
    });
    confirmingCloseRef.current = false;
    if (ok) onClose();
  }

  useImperativeHandle(requestCloseRef, () => requestClose);

  // Every consumer below the row works on the trimmed group, so an excluded task is simply not there.
  const trimmedGroups = useMemo(
    () => groups.map((g) => withoutExcludedTasks(g, excludedTasks.get(g.key) ?? NO_EXCLUDED_TASKS)),
    [groups, excludedTasks],
  );
  const activeGroups = useMemo(
    () => trimmedGroups.filter((g) => !excludedKeys.has(g.key) && g.targets.length > 0),
    [trimmedGroups, excludedKeys],
  );
  // Tasks left without a single group of their own are not part of the batch any more.
  const activeTaskCount = useMemo(
    () => new Set(activeGroups.flatMap((g) => g.targets.map((t) => t.taskId))).size,
    [activeGroups],
  );

  const blockers = activeGroups.map((g) => groupBlocker(g, getState(g.key)));
  const notReady = blockers.filter((b) => b !== "").length;

  function buildFulfillment(
    group: BatchGroup,
    state: GroupState,
    qty: number,
  ): AddFulfillmentRequest {
    const resolved =
      group.catalogItemType === "variation"
        ? {resolvedCatalogItemId: chainLeaf(state.chain)?.id ?? null}
        : {};
    if (state.entries.length > 0) {
      return {sourceNodeId: null, quantity: 0, bundleComponents: state.entries, ...resolved};
    }
    return {sourceNodeId: state.node?.nodeId ?? null, quantity: qty, ...resolved};
  }

  function buildPositions() {
    return activeGroups.flatMap((group) => {
      const state = getState(group.key);
      return group.targets.flatMap((target) => {
        const fulfillment = buildFulfillment(group, state, target.qty);
        // Bundle / Unit fulfillments each count as exactly +1 towards task progress (see
        // countFulfilledQty), so a task needing target.qty of them requires that many identical
        // fulfillments — unlike Standard, where quantity is additive.
        const countsAsOne =
          (fulfillment.bundleComponents?.length ?? 0) > 0 || !!fulfillment.unitInventoryItemId;
        const repeat = countsAsOne ? target.qty : 1;
        return Array.from({length: repeat}, () => ({group, target, fulfillment}));
      });
    });
  }

  const checkItems = checking
    ? buildPositions().map(({group, fulfillment}) => ({
        catalogItemId: group.catalogItemId,
        fulfillment,
      }))
    : [];
  const checkSignature = checking ? JSON.stringify(checkItems) : "";
  const shownCheck = checkResult?.signature === checkSignature ? checkResult : null;
  const checkResultRef = useRef<HTMLDivElement>(null);

  // The answer lands below the groups, often past the fold — bring it into view.
  useEffect(() => {
    if (checkResult) checkResultRef.current?.scrollIntoView({behavior: "smooth", block: "nearest"});
  }, [checkResult]);

  function handleCheck() {
    submittingRef.current = true;
    setSubmitError(null);
    const signature = checkSignature;
    checkMutation.mutate(
      {body: {items: checkItems}},
      {
        onSuccess: (data) =>
          setCheckResult({signature, shortages: data.insufficientInventoryErrors}),
      },
    );
  }

  function handleSubmit() {
    if (submittingRef.current || notReady > 0 || activeGroups.length === 0) return;
    if (checking) {
      handleCheck();
      return;
    }
    submittingRef.current = true;
    setFailedItems([]);
    setShortageErrors([]);
    setSubmitError(null);

    const items = buildPositions().map(({target, fulfillment}) => ({
      orderId: target.orderId,
      taskId: target.taskId,
      taskBoxId: target.taskBoxId,
      componentId: target.componentId,
      fulfillment,
    }));

    // Captured now: once the refetch lands, the groups shrink to what is still missing. The per-call
    // onSuccess below runs only after the hook-level one has awaited that refetch.
    const sentComponentIds = new Set(items.map((i) => i.componentId));
    const allTargets = groups.flatMap((g) => g.targets);
    const coveredTaskIds = new Set(allTargets.map((t) => t.taskId));
    for (const t of allTargets) {
      if (!sentComponentIds.has(t.componentId)) coveredTaskIds.delete(t.taskId);
    }
    const taskByComponent = new Map(items.map((i) => [i.componentId, i.taskId]));

    mutation.mutate(
      {body: {items, autoCompleteTasks: true, allowPartialSuccess}},
      {
        onSuccess: (data) => {
          if (data.failedItems.length === 0 || !allowPartialSuccess) return;
          const failedTaskIds = new Set(
            data.failedItems.map((f) => taskByComponent.get(f.componentId)),
          );
          onPartlyAssembled?.(new Set([...coveredTaskIds].filter((id) => !failedTaskIds.has(id))));
        },
      },
    );
  }

  // Shortages are reported once, folded per item and cell, so the per-group lists leave them out.
  const failedByComponent = useMemo(() => {
    const map = new Map<string, BatchFulfillFailedItem[]>();
    for (const item of failedItems) {
      if (item.error.code === "insufficientInventory") continue;
      const list = map.get(item.componentId) ?? [];
      list.push(item);
      map.set(item.componentId, list);
    }
    return map;
  }, [failedItems]);

  const groupFailures = (group: BatchGroup) =>
    group.targets.flatMap((t) => failedByComponent.get(t.componentId) ?? []);

  const openIndex = trimmedGroups.findIndex((g) => g.key === expandedKey);
  const openGroup = openIndex === -1 ? undefined : trimmedGroups[openIndex];
  const tasksDialogIndex = groups.findIndex((g) => g.key === tasksDialogKey);
  const orderCount = new Set(selectedTasks.map((t) => t.orderId)).size;

  // The phone's composition screen replaces the cards, so nothing else holds the default for the
  // group open on it.
  const mobileOpenGroup = isMobile ? (openGroup ?? null) : null;
  useGroupDefaultNode(
    mobileOpenGroup,
    mobileOpenGroup ? getState(mobileOpenGroup.key) : null,
    patchState,
  );

  const tasksGroup = tasksDialogIndex === -1 ? null : groups[tasksDialogIndex];
  const tasksDialog = tasksGroup && (
    <GroupTasksDialog
      open
      onClose={() => setTasksDialogKey(null)}
      group={tasksGroup}
      excludedTaskIds={excludedTasks.get(tasksGroup.key) ?? NO_EXCLUDED_TASKS}
      onToggleTask={(taskId) => toggleTaskExcluded(tasksGroup.key, taskId)}
    />
  );

  // Kept outside the todo registry: rows nobody can see must not be what the counter jumps to.
  const headless = (
    <Box sx={{display: "none"}}>
      {activeGroups
        .filter((g) => g.key !== expandedKey && getState(g.key).hinted)
        .map((g) => (
          <HeadlessComposition key={g.key} group={g} state={getState(g.key)} onPatch={patchState} />
        ))}
    </Box>
  );

  // A check wants the shelves' answer, not the dialog's guess, so a short cell never holds it back.
  const stockIgnored = checking || ignoreStock;

  // On a phone the composition takes the whole dialog instead of unfolding inside a row.
  if (isMobile && openGroup) {
    return (
      <IgnoreStockContext value={stockIgnored}>
        <DialogTitle sx={{pb: 1}}>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <IconButton size="small" onClick={() => setExpandedKey(null)}>
              <ArrowBackIcon fontSize="small" />
            </IconButton>
            <Stack>
              <Typography variant="h6">{openGroup.catalogItemName}</Typography>
              <Typography variant="caption" color="text.secondary">
                состав ×1 · применится к {pluralCount(openGroup.targets.length, TASKS_DATIVE)}
              </Typography>
            </Stack>
          </Stack>
        </DialogTitle>
        <DialogContent>
          <TodoRegistryProvider registry={registry}>
            <Stack spacing={1} sx={{mt: 1}}>
              <GroupTasksSummary
                group={openGroup}
                fullGroup={groups[openIndex]}
                onOpenTasks={() => setTasksDialogKey(openGroup.key)}
              />
              {openGroup.targets.length === 0 ? (
                <EmptiedGroupNote />
              ) : (
                <Box>
                  <GroupComposition
                    group={openGroup}
                    state={getState(openGroup.key)}
                    onPatch={patchState}
                    onPicksPatch={patchPicks}
                  />
                </Box>
              )}
            </Stack>
          </TodoRegistryProvider>
          {headless}
          {tasksDialog}
        </DialogContent>
        <DialogActions sx={{flexDirection: "column", alignItems: "stretch", gap: 1}}>
          <UnfilledCounter status={getState(openGroup.key).status} onJump={scrollToFirst} />
          <Button fullWidth variant="contained" onClick={() => setExpandedKey(null)}>
            Готово
          </Button>
        </DialogActions>
      </IgnoreStockContext>
    );
  }

  return (
    <IgnoreStockContext value={stockIgnored}>
      <DialogTitle sx={{pb: 1}}>
        <Stack spacing={0.5}>
          <Typography variant="h6">
            {checking ? "Получится ли собрать?" : "Массовая сборка"}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {checking
              ? pluralCount(orderCount, ORDERS)
              : `${pluralCount(selectedTasks.length, NOUNS.task)} · ${pluralCount(orderCount, ORDERS)}`}
          </Typography>
        </Stack>
      </DialogTitle>

      <DialogContent>
        <TodoRegistryProvider registry={registry}>
          <Stack spacing={2} sx={{mt: 1}}>
            {notice && <Alert severity="info">{notice}</Alert>}

            {groups.length === 0 && (
              <Typography color="text.secondary">
                {checking
                  ? "В выбранных заказах нечего проверять"
                  : "В выбранных заданиях не осталось несобранных позиций"}
              </Typography>
            )}

            {groups.length > 0 &&
              (isMobile ? (
                <Stack spacing={1}>
                  {trimmedGroups.map((group, i) => (
                    <GroupCard
                      key={group.key}
                      group={group}
                      fullGroup={groups[i]}
                      state={getState(group.key)}
                      blocker={groupBlocker(group, getState(group.key))}
                      loading={groupLoading(group)}
                      failures={groupFailures(group)}
                      excluded={excludedKeys.has(group.key)}
                      onPatch={patchState}
                      onToggleExcluded={() => toggleExcluded(group.key)}
                      onOpenTasks={() => setTasksDialogKey(group.key)}
                      onOpenComposition={() => setExpandedKey(group.key)}
                      onReset={() => resetGroup(group.key)}
                    />
                  ))}
                </Stack>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Позиция</TableCell>
                      <TableCell align="right">Итого</TableCell>
                      <TableCell>Источник</TableCell>
                      <TableCell sx={{width: STATUS_COLUMN_WIDTH}}>Статус</TableCell>
                      <TableCell padding="checkbox" />
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {trimmedGroups.map((group, i) => (
                      <GroupRow
                        key={group.key}
                        group={group}
                        fullGroup={groups[i]}
                        state={getState(group.key)}
                        blocker={groupBlocker(group, getState(group.key))}
                        loading={groupLoading(group)}
                        failures={groupFailures(group)}
                        excluded={excludedKeys.has(group.key)}
                        expanded={expandedKey === group.key}
                        onToggle={() =>
                          setExpandedKey((prev) => (prev === group.key ? null : group.key))
                        }
                        onToggleExcluded={() => toggleExcluded(group.key)}
                        onOpenTasks={() => setTasksDialogKey(group.key)}
                        onPatch={patchState}
                        onPicksPatch={patchPicks}
                        onReset={() => resetGroup(group.key)}
                      />
                    ))}
                  </TableBody>
                </Table>
              ))}

            {shortageErrors.length > 0 && (
              <Alert severity="error">
                <AlertTitle>Не хватило остатков</AlertTitle>
                {shortageErrors.map((error, i) => (
                  <Typography key={i} variant="caption" sx={{display: "block"}}>
                    • {resolveErrorMessage(error)}
                  </Typography>
                ))}
              </Alert>
            )}

            {shownCheck && shownCheck.shortages.length === 0 && (
              <Alert ref={checkResultRef} severity="success">
                Остатков хватит на {pluralCount(activeTaskCount, NOUNS.task)}
              </Alert>
            )}
            {shownCheck && shownCheck.shortages.length > 0 && (
              <Alert ref={checkResultRef} severity="error">
                <AlertTitle>Не хватит остатков</AlertTitle>
                {shownCheck.shortages.map((error, i) => (
                  <Typography key={i} variant="caption" sx={{display: "block"}}>
                    • {resolveErrorMessage(error)}
                  </Typography>
                ))}
              </Alert>
            )}

            {submitError && <Alert severity="error">{submitError}</Alert>}

            {!checking && (
              <Stack>
                <FormControlLabel
                  control={
                    <Checkbox
                      size="small"
                      checked={allowPartialSuccess}
                      onChange={(e) => setAllowPartialSuccess(e.target.checked)}
                    />
                  }
                  label={
                    <Typography variant="body2">Сохранять успешные позиции при ошибках</Typography>
                  }
                />
                <Typography variant="caption" color="text.secondary" sx={{ml: 4, mt: -0.5}}>
                  {allowPartialSuccess
                    ? "Что удалось собрать — останется собранным"
                    : "Любая ошибка откатит всю партию целиком"}
                </Typography>
                {import.meta.env.DEV && (
                  <FormControlLabel
                    sx={{mt: 0.5}}
                    control={
                      <Switch
                        size="small"
                        checked={ignoreStock}
                        onChange={(e) => setIgnoreStock(e.target.checked)}
                      />
                    }
                    label={
                      <Typography variant="body2" color="text.secondary">
                        dev: не блокировать сборку при нехватке остатков
                      </Typography>
                    }
                  />
                )}
              </Stack>
            )}
          </Stack>
        </TodoRegistryProvider>
        {headless}
        {tasksDialog}
      </DialogContent>

      <DialogActions sx={{gap: 1, flexDirection: isMobile ? "column-reverse" : "row"}}>
        {notReady > 0 && (
          <Typography variant="caption" color="error">
            Не готово: {pluralCount(notReady, GROUPS)}
          </Typography>
        )}
        {notReady === 0 && groups.length > 0 && activeGroups.length === 0 && (
          <Typography variant="caption" color="text.secondary">
            Все группы убраны из сборки
          </Typography>
        )}
        <Stack
          direction={isMobile ? "column-reverse" : "row"}
          spacing={1}
          sx={{ml: isMobile ? 0 : "auto", width: isMobile ? "100%" : undefined}}
        >
          <Button onClick={() => void requestClose()} disabled={isPending} fullWidth={isMobile}>
            {checking ? "Закрыть" : "Отмена"}
          </Button>
          <Button
            variant="contained"
            fullWidth={isMobile}
            onClick={handleSubmit}
            disabled={isPending || activeGroups.length === 0 || notReady > 0}
          >
            {isPending ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              `${checking ? "Проверить" : "Собрать"} ${pluralCount(activeTaskCount, NOUNS.task)}`
            )}
          </Button>
        </Stack>
      </DialogActions>
    </IgnoreStockContext>
  );
}

const ORDERS = {one: "заказ", few: "заказа", many: "заказов"};

const GROUPS = {one: "группа", few: "группы", many: "групп"};

// после «к» задание встаёт в дательный падеж
const TASKS_DATIVE = {one: "заданию", few: "заданиям", many: "заданиям"};

interface GroupViewProps {
  group: BatchGroup;
  state: GroupState;
  blocker: string;
  failures: BatchFulfillFailedItem[];
  onPatch: (key: string, patch: Partial<GroupState>) => void;
}

const taskCount = (group: BatchGroup) => new Set(group.targets.map((t) => t.taskId)).size;

interface GroupTasksSummaryProps {
  /** Trimmed to the tasks still in the batch; `fullGroup` is what was selected before exclusions. */
  group: BatchGroup;
  fullGroup: BatchGroup;
  onOpenTasks: () => void;
}

function GroupTasksSummary({group, fullGroup, onOpenTasks}: GroupTasksSummaryProps) {
  const excluded = fullGroup.totalNeeded - group.totalNeeded;

  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap", rowGap: 0.5}}>
      <Typography variant="caption" color="text.secondary">
        Позиций: {fullGroup.totalNeeded} · исключено {excluded} · осталось {group.totalNeeded}
      </Typography>
      <Button size="small" onClick={onOpenTasks}>
        Задания ({taskCount(group)} из {taskCount(fullGroup)})
      </Button>
    </Stack>
  );
}

/** A group with every task taken out has nothing left to compose — its picks would go nowhere. */
function EmptiedGroupNote() {
  return (
    <Typography variant="body2" color="text.secondary">
      Все задания этой позиции убраны из сборки
    </Typography>
  );
}

const STATUS_COLUMN_WIDTH = 140;

// What a composition tree loads on its own; the page under the dialog fetches plenty of anything else.
const TREE_QUERY_IDS: ReadonlySet<string> = new Set([
  "catalogGetById",
  "storagePlacesGetNodeItemCount",
  "warehousesGetDefaultNode",
]);

function isTreeQuery(query: Query): boolean {
  const head = query.queryKey[0];
  return (
    typeof head === "object" &&
    head !== null &&
    TREE_QUERY_IDS.has((head as {_id?: string})._id ?? "")
  );
}

/**
 * Whether anything is fetching, held a moment past the last request: a nested tree level only starts
 * its query after the parent's lands, and the gap between them must not read as «done».
 */
function useSettledFetching(): boolean {
  const fetching = useIsFetching({predicate: isTreeQuery}) > 0;
  const [settled, setSettled] = useState(!fetching);
  if (fetching && settled) setSettled(false);

  useEffect(() => {
    if (fetching) return;
    const timer = setTimeout(() => setSettled(true), 300);
    return () => clearTimeout(timer);
  }, [fetching]);

  return fetching || !settled;
}

function StatusChip({blocker, loading}: {blocker: string; loading: boolean}) {
  if (loading) {
    return (
      <Chip
        size="small"
        variant="outlined"
        icon={<CircularProgress size={12} color="inherit" />}
        label="загрузка"
      />
    );
  }
  return blocker ? (
    <Chip size="small" color="warning" label={blocker} />
  ) : (
    <Chip size="small" color="success" label="готово" />
  );
}

interface SourceCellProps extends Omit<GroupViewProps, "blocker" | "failures"> {
  /** The card picks the cell in place; the table row only reports it and picks inside the panel. */
  withPicker?: boolean;
}

function SourceCell({group, state, onPatch, withPicker}: SourceCellProps) {
  const resolved = resolvedItem(group, state);

  if (withPicker && group.catalogItemType === "standard") {
    return (
      <GroupNodeControl
        group={group}
        state={state}
        onPatch={onPatch}
        catalogItemId={group.catalogItemId}
      />
    );
  }
  if (!resolved) {
    return (
      <Typography variant="caption" color="text.secondary">
        вариант не выбран
      </Typography>
    );
  }
  // Everything else is picked inside the composition panel; the row only reports where it stands.
  if (resolved.type === "standard") {
    return (
      <Typography variant="caption" color={state.node ? "text.primary" : "text.secondary"}>
        {state.node ? state.node.nodePath : "нужна ячейка"}
      </Typography>
    );
  }
  const ready = isComplete(state.status);
  return (
    <Typography variant="caption" color={ready ? "text.primary" : "text.secondary"}>
      {ready ? "состав задан" : "состав задаётся"}
    </Typography>
  );
}

interface GroupNodeControlProps extends Omit<GroupViewProps, "blocker" | "failures"> {
  catalogItemId: string;
}

/**
 * The group submits `state.node`, so the warehouse default has to land there — a suggestion the pick
 * overrides. It lives above the picker because the picker is only mounted while the group is open,
 * and a cell the user never touched still has to be there when the batch goes out. The picker reads
 * the same default for its «по умолчанию» mark; both readers share one query.
 */
function useGroupDefaultNode(
  group: BatchGroup | null,
  state: GroupState | null,
  onPatch: (key: string, patch: Partial<GroupState>) => void,
) {
  const wanted = !!group && !!state && resolvedItem(group, state)?.type === "standard";
  const {node: defaultNode, isPending} = useDefaultStorageNodeQuery(
    group?.warehouseId ?? "",
    wanted,
  );
  const node = state?.node;
  const groupKey = group?.key;

  useEffect(() => {
    if (!wanted || node || !defaultNode || !groupKey) return;
    onPatch(groupKey, {
      node: {
        nodeId: defaultNode.nodeId,
        nodePath: formatStoragePlaceNodeName(defaultNode.nodePath),
      },
    });
  }, [wanted, defaultNode, node, groupKey, onPatch]);

  // true until the default lands in the state, including the render between its arrival and the patch
  return wanted && !node && !state?.nodePicked && (isPending || !!defaultNode);
}

/** Cell of a standard group — the need is the whole group, so the stock check is multiplied. */
function GroupNodeControl({group, state, onPatch, catalogItemId}: GroupNodeControlProps) {
  const defaultNode = useDefaultStorageNode(group.warehouseId);
  const node = state.node;
  const available = useNodeItemCount(node?.nodeId, catalogItemId);

  return (
    <NodeControl
      warehouseId={group.warehouseId}
      catalogItemId={catalogItemId}
      node={node}
      isDefault={!state.nodePicked && !!defaultNode}
      available={available}
      needQty={group.totalNeeded}
      onSelect={(picked) => onPatch(group.key, {node: picked, nodePicked: true})}
    />
  );
}

interface GroupRowProps extends GroupViewProps {
  loading: boolean;
  fullGroup: BatchGroup;
  excluded: boolean;
  expanded: boolean;
  onToggle: () => void;
  onToggleExcluded: () => void;
  onOpenTasks: () => void;
  onPicksPatch: (key: string, update: PicksUpdate) => void;
  onReset: () => void;
}

function GroupRow({
  group,
  fullGroup,
  state,
  blocker,
  loading,
  failures,
  excluded,
  expanded,
  onToggle,
  onToggleExcluded,
  onOpenTasks,
  onPatch,
  onPicksPatch,
  onReset,
}: GroupRowProps) {
  const composable = group.catalogItemType !== "standard";
  const emptied = !excluded && group.targets.length === 0;

  const settlingCell = useGroupDefaultNode(group, state, onPatch);

  return (
    <>
      <TableRow
        hover={!excluded}
        onClick={excluded ? undefined : onToggle}
        sx={{
          cursor: excluded ? "default" : "pointer",
          opacity: excluded ? 0.5 : undefined,
          "& > td": {borderBottom: expanded ? 0 : undefined},
        }}
      >
        <TableCell>
          <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
            {/* The whole row toggles, so the twist is an affordance — its click just bubbles up. */}
            <IconButton size="small" tabIndex={-1} disabled={excluded}>
              {expanded ? (
                <KeyboardArrowDownIcon fontSize="small" />
              ) : (
                <KeyboardArrowRightIcon fontSize="small" />
              )}
            </IconButton>
            <Typography
              variant="body2"
              sx={{textDecoration: excluded ? "line-through" : undefined}}
            >
              {group.catalogItemName}
            </Typography>
            {composable && !excluded && (
              <Chip
                size="small"
                variant="outlined"
                label={
                  group.targets.length > 1
                    ? `состав ×1 на ${pluralCount(group.targets.length, NOUNS.task)}`
                    : "состав ×1"
                }
              />
            )}
            {state.hinted && !excluded && <HintChip onReset={onReset} />}
          </Stack>
        </TableCell>
        <TableCell align="right">
          {group.totalNeeded}
          {fullGroup.totalNeeded !== group.totalNeeded && (
            <Typography variant="caption" color="text.secondary">
              {" "}
              из {fullGroup.totalNeeded}
            </Typography>
          )}
        </TableCell>
        <TableCell>
          {excluded || emptied ? (
            <Typography variant="caption" color="text.secondary">
              —
            </Typography>
          ) : (
            <SourceCell group={group} state={state} onPatch={onPatch} />
          )}
        </TableCell>
        <TableCell>
          {excluded && <Chip size="small" label="убрана" />}
          {emptied && <Chip size="small" label="нет заданий" />}
          {!excluded && !emptied && (
            <StatusChip blocker={blocker} loading={loading || settlingCell} />
          )}
        </TableCell>
        {/* Taking the group out must not also fold the row shut. */}
        <TableCell padding="checkbox" onClick={(e) => e.stopPropagation()}>
          <Tooltip title={excluded ? "Вернуть в сборку" : "Убрать из сборки"}>
            <IconButton size="small" onClick={onToggleExcluded}>
              {excluded ? <UndoIcon fontSize="small" /> : <CloseIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
        </TableCell>
      </TableRow>

      {expanded && !excluded && (
        <TableRow>
          <TableCell colSpan={5} sx={{pt: 0}}>
            <Stack spacing={1.5} sx={{pt: 0.5}}>
              <GroupTasksSummary group={group} fullGroup={fullGroup} onOpenTasks={onOpenTasks} />
              {emptied && <EmptiedGroupNote />}
              {!composable && !emptied && (
                <Paper variant="outlined" sx={{p: 1.5, bgcolor: "action.hover"}}>
                  <GroupNodeControl
                    group={group}
                    state={state}
                    onPatch={onPatch}
                    catalogItemId={group.catalogItemId}
                  />
                </Paper>
              )}
              {composable && !emptied && (
                <GroupComposition
                  group={group}
                  state={state}
                  onPatch={onPatch}
                  onPicksPatch={onPicksPatch}
                />
              )}
            </Stack>
          </TableCell>
        </TableRow>
      )}

      {failures.length > 0 && (
        <TableRow>
          <TableCell colSpan={5} sx={{py: 0.5}}>
            <Alert severity="error" sx={{py: 0}}>
              {failures.map((f, i) => (
                <Typography key={i} variant="caption" sx={{display: "block"}}>
                  • {resolveErrorMessage(f.error)}
                </Typography>
              ))}
            </Alert>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

interface GroupCardProps extends GroupViewProps {
  loading: boolean;
  fullGroup: BatchGroup;
  excluded: boolean;
  onToggleExcluded: () => void;
  onOpenTasks: () => void;
  onOpenComposition: () => void;
  onReset: () => void;
}

function GroupCard({
  group,
  fullGroup,
  state,
  blocker,
  loading,
  failures,
  excluded,
  onPatch,
  onToggleExcluded,
  onOpenTasks,
  onOpenComposition,
  onReset,
}: GroupCardProps) {
  const composable = group.catalogItemType !== "standard";
  const emptied = !excluded && group.targets.length === 0;

  const settlingCell = useGroupDefaultNode(group, state, onPatch);

  return (
    <Paper variant="outlined" sx={{p: 1.5, opacity: excluded ? 0.5 : undefined}}>
      <Stack spacing={1}>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", justifyContent: "space-between"}}
        >
          <Typography
            variant="body2"
            sx={{fontWeight: 500, textDecoration: excluded ? "line-through" : undefined}}
          >
            {group.catalogItemName}
          </Typography>
          <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
            {excluded && <Chip size="small" label="убрана" />}
            {emptied && <Chip size="small" label="нет заданий" />}
            {!excluded && !emptied && (
              <StatusChip blocker={blocker} loading={loading || settlingCell} />
            )}
            <IconButton
              size="small"
              aria-label={excluded ? "Вернуть в сборку" : "Убрать из сборки"}
              onClick={onToggleExcluded}
            >
              {excluded ? <UndoIcon fontSize="small" /> : <CloseIcon fontSize="small" />}
            </IconButton>
          </Stack>
        </Stack>
        <Stack
          direction="row"
          spacing={1}
          sx={{alignItems: "center", flexWrap: "wrap", rowGap: 0.5}}
        >
          <Typography variant="caption" color="text.secondary">
            {group.totalNeeded} шт · {pluralCount(taskCount(group), NOUNS.task)}
          </Typography>
          {!excluded && !emptied && (
            <SourceCell group={group} state={state} onPatch={onPatch} withPicker />
          )}
          {state.hinted && !excluded && <HintChip onReset={onReset} />}
        </Stack>
        {!excluded && (
          <GroupTasksSummary group={group} fullGroup={fullGroup} onOpenTasks={onOpenTasks} />
        )}
        {composable && !excluded && !emptied && (
          <Button size="small" variant="outlined" fullWidth onClick={onOpenComposition}>
            Задать состав
          </Button>
        )}
        {failures.length > 0 && (
          <Alert severity="error" sx={{py: 0}}>
            {failures.map((f, i) => (
              <Typography key={i} variant="caption" sx={{display: "block"}}>
                • {resolveErrorMessage(f.error)}
              </Typography>
            ))}
          </Alert>
        )}
      </Stack>
    </Paper>
  );
}

/** The composition is filled once and copied to every task of the group. */
interface GroupCompositionProps extends Omit<GroupViewProps, "blocker" | "failures"> {
  onPicksPatch: (key: string, update: PicksUpdate) => void;
}

function GroupComposition({group, state, onPatch, onPicksPatch}: GroupCompositionProps) {
  const handleTreeChange = useCallback(
    (entries: AddFulfillmentBundleComponentRequest[], status: SlotStatus) =>
      onPatch(group.key, {entries, status}),
    [group.key, onPatch],
  );

  const resolved = resolvedItem(group, state);

  // Bundle fulfillments count one apiece, so the group needs `totalNeeded` copies of the composition;
  // a standard item is additive and goes out as a quantity instead.
  const writeOff =
    resolved?.type === "standard"
      ? `спишется ${group.totalNeeded} шт`
      : `спишется ${group.totalNeeded} × состав`;

  return (
    <CompositionFrame group={group} status={state.status} writeOff={writeOff}>
      {group.catalogItemType === "variation" && (
        <VariationChain
          rootCatalogItemId={group.catalogItemId}
          chain={state.chain}
          onChange={(chain) =>
            onPatch(group.key, {
              chain,
              node: null,
              nodePicked: false,
              entries: [],
              status: EMPTY_STATUS,
              picks: NO_PICKS,
              hinted: false,
            })
          }
        />
      )}

      {resolved?.type === "standard" && (
        <GroupNodeControl
          group={group}
          state={state}
          onPatch={onPatch}
          catalogItemId={resolved.id}
        />
      )}

      {resolved?.type === "bundle" && (
        <BundleTree
          key={resolved.id}
          catalogItemId={resolved.id}
          warehouseId={group.warehouseId}
          needTimes={group.totalNeeded}
          picks={state.picks}
          onPicksChange={(update) => onPicksPatch(group.key, update)}
          onChange={handleTreeChange}
        />
      )}
    </CompositionFrame>
  );
}

/**
 * A hinted bundle is filled without anyone unfolding it, yet its entries and status only come out of a
 * mounted tree — so one runs out of sight until the group is opened and the visible tree takes over.
 */
function HeadlessComposition({
  group,
  state,
  onPatch,
}: Pick<GroupViewProps, "group" | "state" | "onPatch">) {
  // slots report from effects, so a fresh callback every render would loop them
  const handleTreeChange = useCallback(
    (entries: AddFulfillmentBundleComponentRequest[], status: SlotStatus) =>
      onPatch(group.key, {entries, status}),
    [group.key, onPatch],
  );

  const resolved = resolvedItem(group, state);
  if (resolved?.type !== "bundle") return null;

  return (
    <BundleTree
      key={resolved.id}
      catalogItemId={resolved.id}
      warehouseId={group.warehouseId}
      needTimes={group.totalNeeded}
      picks={state.picks}
      onPicksChange={() => {}}
      onChange={handleTreeChange}
    />
  );
}

function HintChip({onReset}: {onReset: () => void}) {
  return (
    <Tooltip title="Варианты взяты из вашей прошлой сборки этой позиции">
      <Chip
        size="small"
        color="info"
        variant="outlined"
        label="как в прошлый раз"
        // the row folds open on any click, the reset must not
        onDelete={(e: React.SyntheticEvent) => {
          e.stopPropagation();
          onReset();
        }}
      />
    </Tooltip>
  );
}

interface CompositionFrameProps {
  group: BatchGroup;
  status: SlotStatus;
  writeOff: string;
  children: React.ReactNode;
}

function CompositionFrame({group, status, writeOff, children}: CompositionFrameProps) {
  return (
    <Paper variant="outlined" sx={{p: 1.5, bgcolor: "action.hover"}}>
      <Stack spacing={1}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap"}}>
          <Chip size="small" color="warning" label="состав ×1" />
          <Typography variant="caption" color="text.secondary">
            применится к {pluralCount(group.targets.length, TASKS_DATIVE)} · {writeOff}
          </Typography>
          {status.total > 0 && !isComplete(status) && (
            <Chip size="small" color="error" label={`${status.filled} из ${status.total}`} />
          )}
        </Stack>
        {children}
      </Stack>
    </Paper>
  );
}

export default BatchAssemblyDialog;
