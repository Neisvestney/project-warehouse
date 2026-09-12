import {useCallback, useEffect, useMemo, useRef, useState} from "react";
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
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  ordersBatchFulfillMutation,
  ordersGetAllAssemblyQueryKey,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {
  AddFulfillmentBundleComponentRequest,
  AddFulfillmentRequest,
  AppFieldError,
  BatchFulfillFailedItem,
  CatalogItemType,
} from "@/api/types.gen";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useDefaultStorageNode} from "@/hooks/useDefaultStorageNode";
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
  buildBatchGroups,
  withoutExcludedTasks,
  NO_EXCLUDED_TASKS,
  type BatchGroup,
  type SelectedTaskInfo,
} from "./batchGroups";
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
}

function emptyGroupState(): GroupState {
  return {
    node: null,
    nodePicked: false,
    chain: [],
    entries: [],
    status: EMPTY_STATUS,
    picks: NO_PICKS,
  };
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

interface BatchAssemblyDialogProps {
  open: boolean;
  onClose: () => void;
  selectedTasks: SelectedTaskInfo[];
}

function BatchAssemblyDialog({open, onClose, selectedTasks}: BatchAssemblyDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  // The content is unmounted only after the exit animation; that is what resets the per-group picks.
  const [shownTasks, releaseShownTasks] = useRetainedValue(open ? selectedTasks : null);

  // Closing goes through the dialog's own Cancel button only — no backdrop, no Esc, no Back.
  useBackClosable(open, onClose, {blockBack: true});

  return (
    <Dialog
      open={open}
      maxWidth="md"
      fullWidth
      fullScreen={isMobile}
      slotProps={{
        transition: {onExited: releaseShownTasks},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownTasks && (
        <BatchAssemblyContent onClose={onClose} selectedTasks={shownTasks} isMobile={isMobile} />
      )}
    </Dialog>
  );
}

function BatchAssemblyContent({
  onClose,
  selectedTasks,
  isMobile,
}: Omit<BatchAssemblyDialogProps, "open"> & {isMobile: boolean}) {
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();
  const groups = useMemo(() => buildBatchGroups(selectedTasks), [selectedTasks]);
  const {registry, scrollToFirst} = useTodoRegistry();

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
  const submittingRef = useRef(false);

  const getState = useCallback(
    (key: string) => groupStates.get(key) ?? emptyGroupState(),
    [groupStates],
  );

  const patchState = useCallback((key: string, patch: Partial<GroupState>) => {
    setGroupStates((prev) =>
      new Map(prev).set(key, {...(prev.get(key) ?? emptyGroupState()), ...patch}),
    );
  }, []);

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
      return new Map(prev).set(key, {...state, picks: update(state.picks)});
    });
  }, []);

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
      ]);
      setFailedItems(data.failedItems);
      setShortageErrors(data.insufficientInventoryErrors);
      const outcome = describeOutcome(
        variables.body.items.length,
        data.failedItems.length,
        variables.body.allowPartialSuccess,
      );
      enqueueSnackbar(outcome.message, {variant: outcome.variant});
      if (data.failedItems.length === 0) onClose();
    },
    onError: (error) => setSubmitError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

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

  function handleSubmit() {
    if (submittingRef.current || notReady > 0 || activeGroups.length === 0) return;
    submittingRef.current = true;
    setFailedItems([]);
    setShortageErrors([]);
    setSubmitError(null);

    const items = activeGroups.flatMap((group) => {
      const state = getState(group.key);
      return group.targets.flatMap((target) => {
        const fulfillment = buildFulfillment(group, state, target.qty);
        // Bundle / Unit fulfillments each count as exactly +1 towards task progress (see
        // countFulfilledQty), so a task needing target.qty of them requires that many identical
        // fulfillments — unlike Standard, where quantity is additive.
        const countsAsOne =
          (fulfillment.bundleComponents?.length ?? 0) > 0 || !!fulfillment.unitInventoryItemId;
        const repeat = countsAsOne ? target.qty : 1;
        return Array.from({length: repeat}, () => ({
          orderId: target.orderId,
          taskId: target.taskId,
          taskBoxId: target.taskBoxId,
          componentId: target.componentId,
          fulfillment,
        }));
      });
    });

    mutation.mutate({body: {items, autoCompleteTasks: true, allowPartialSuccess}});
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

  // On a phone the composition takes the whole dialog instead of unfolding inside a row.
  if (isMobile && openGroup) {
    return (
      <IgnoreStockContext value={ignoreStock}>
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
    <IgnoreStockContext value={ignoreStock}>
      <DialogTitle sx={{pb: 1}}>
        <Stack spacing={0.5}>
          <Typography variant="h6">Массовая сборка</Typography>
          <Typography variant="caption" color="text.secondary">
            {pluralCount(selectedTasks.length, NOUNS.task)} · {pluralCount(orderCount, ORDERS)}
          </Typography>
        </Stack>
      </DialogTitle>

      <DialogContent>
        <TodoRegistryProvider registry={registry}>
          <Stack spacing={2} sx={{mt: 1}}>
            {groups.length === 0 && (
              <Typography color="text.secondary">
                В выбранных заданиях не осталось несобранных позиций
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
                      failures={groupFailures(group)}
                      excluded={excludedKeys.has(group.key)}
                      onPatch={patchState}
                      onToggleExcluded={() => toggleExcluded(group.key)}
                      onOpenTasks={() => setTasksDialogKey(group.key)}
                      onOpenComposition={() => setExpandedKey(group.key)}
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
                      <TableCell>Статус</TableCell>
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

            {submitError && <Alert severity="error">{submitError}</Alert>}

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
          </Stack>
        </TodoRegistryProvider>
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
          <Button onClick={onClose} disabled={mutation.isPending} fullWidth={isMobile}>
            Отмена
          </Button>
          <Button
            variant="contained"
            fullWidth={isMobile}
            onClick={handleSubmit}
            disabled={mutation.isPending || activeGroups.length === 0 || notReady > 0}
          >
            {mutation.isPending ? (
              <CircularProgress size={20} color="inherit" />
            ) : (
              `Собрать ${pluralCount(activeTaskCount, NOUNS.task)}`
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

function StatusChip({blocker}: {blocker: string}) {
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
  const defaultNode = useDefaultStorageNode(group?.warehouseId ?? "", wanted);
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
  fullGroup: BatchGroup;
  excluded: boolean;
  expanded: boolean;
  onToggle: () => void;
  onToggleExcluded: () => void;
  onOpenTasks: () => void;
  onPicksPatch: (key: string, update: PicksUpdate) => void;
}

function GroupRow({
  group,
  fullGroup,
  state,
  blocker,
  failures,
  excluded,
  expanded,
  onToggle,
  onToggleExcluded,
  onOpenTasks,
  onPatch,
  onPicksPatch,
}: GroupRowProps) {
  const composable = group.catalogItemType !== "standard";
  const emptied = !excluded && group.targets.length === 0;

  useGroupDefaultNode(group, state, onPatch);

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
          {!excluded && !emptied && <StatusChip blocker={blocker} />}
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
  fullGroup: BatchGroup;
  excluded: boolean;
  onToggleExcluded: () => void;
  onOpenTasks: () => void;
  onOpenComposition: () => void;
}

function GroupCard({
  group,
  fullGroup,
  state,
  blocker,
  failures,
  excluded,
  onPatch,
  onToggleExcluded,
  onOpenTasks,
  onOpenComposition,
}: GroupCardProps) {
  const composable = group.catalogItemType !== "standard";
  const emptied = !excluded && group.targets.length === 0;

  useGroupDefaultNode(group, state, onPatch);

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
            {!excluded && !emptied && <StatusChip blocker={blocker} />}
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
