import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersBatchFulfillMutation,
  ordersGetAllAssemblyQueryKey,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {
  AddFulfillmentBundleComponentRequest,
  AddFulfillmentRequest,
  AssemblyTaskDto,
  BatchFulfillFailedItem,
  CatalogItemType,
} from "@/api/types.gen";
import SelectNodeModal, {type SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDefaultStorageNode} from "@/hooks/useDefaultStorageNode";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {BundleTreeForm, VariationForm} from "./AddFulfillmentDialog";
import {getRemainingQty} from "./batchEligibility";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import {useRetainedValue} from "@/hooks/useRetainedValue";

interface SelectedTaskInfo {
  orderId: string;
  taskId: string;
  task: AssemblyTaskDto;
  warehouseId: string;
}

interface BatchGroup {
  key: string;
  catalogItemId: string;
  catalogItemName: string;
  catalogItemType: CatalogItemType;
  warehouseId: string;
  totalNeeded: number;
  taskComponents: {
    orderId: string;
    taskId: string;
    taskBoxId: string;
    componentId: string;
    qty: number;
  }[];
}

function buildBatchGroups(selectedTasks: SelectedTaskInfo[]): BatchGroup[] {
  const groupMap = new Map<string, BatchGroup>();

  for (const {orderId, taskId, task, warehouseId} of selectedTasks) {
    for (const box of task.boxes) {
      for (const comp of box.components) {
        const remaining = getRemainingQty(comp);
        if (remaining <= 0) continue;

        const key = `${comp.catalogItemId}::${warehouseId}`;
        const existing = groupMap.get(key);
        const entry = {
          orderId,
          taskId,
          taskBoxId: box.id,
          componentId: comp.id,
          qty: remaining,
        };
        if (existing) {
          existing.totalNeeded += remaining;
          existing.taskComponents.push(entry);
        } else {
          groupMap.set(key, {
            key,
            catalogItemId: comp.catalogItemId,
            catalogItemName: comp.catalogItemName,
            catalogItemType: comp.catalogItemType as CatalogItemType,
            warehouseId,
            totalNeeded: remaining,
            taskComponents: [entry],
          });
        }
      }
    }
  }

  return Array.from(groupMap.values());
}

// ─── Node picker row ──────────────────────────────────────────────────────

interface NodePickerRowProps {
  label: string;
  warehouseId: string;
  value: {nodeId: string | null; nodePath: string | null};
  onChange: (node: SelectedNode) => void;
  catalogItemId?: string;
}

function NodePickerRow({label, warehouseId, value, onChange, catalogItemId}: NodePickerRowProps) {
  const [open, setOpen] = useState(false);
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <Typography variant="caption" sx={{minWidth: 120}}>
        {label}
      </Typography>
      <Typography
        variant="body2"
        sx={{flex: 1, color: value.nodePath ? "text.primary" : "text.disabled"}}
      >
        {value.nodePath ?? "Не выбрано"}
      </Typography>
      <Button
        variant={"outlined"}
        size="small"
        startIcon={<LocationOnIcon />}
        onClick={() => setOpen(true)}
      >
        Выбрать
      </Button>
      <SelectNodeModal
        open={open}
        onClose={() => setOpen(false)}
        warehouseId={warehouseId}
        onSelect={(node) => {
          onChange(node);
          setOpen(false);
        }}
        catalogItemId={catalogItemId}
      />
    </Stack>
  );
}

// ─── Group form by type ───────────────────────────────────────────────────

interface StandardGroupFormProps {
  group: BatchGroup;
  value: {nodeId: string | null; nodePath: string | null};
  onChange: (v: {nodeId: string | null; nodePath: string | null}) => void;
}

function StandardGroupForm({group, value, onChange}: StandardGroupFormProps) {
  const defaultNode = useDefaultStorageNode(group.warehouseId);
  useEffect(() => {
    if (defaultNode && !value.nodeId) {
      onChange({
        nodeId: defaultNode.nodeId,
        nodePath: formatStoragePlaceNodeName(defaultNode.nodePath),
      });
    }
  }, [defaultNode, value.nodeId, onChange]);

  return (
    <Stack spacing={1}>
      <Typography variant="body2">
        <strong>{group.catalogItemName}</strong> — итого: {group.totalNeeded} шт.
      </Typography>
      <NodePickerRow
        label="Ячейка"
        warehouseId={group.warehouseId}
        value={value}
        onChange={(node) =>
          onChange({nodeId: node.nodeId, nodePath: formatStoragePlaceNodeName(node.nodePath)})
        }
        catalogItemId={group.catalogItemId}
      />
    </Stack>
  );
}

interface BundleGroupFormProps {
  group: BatchGroup;
  onChange: (v: AddFulfillmentBundleComponentRequest[], complete: boolean) => void;
}

function BundleGroupForm({group, onChange}: BundleGroupFormProps) {
  return (
    <Stack spacing={2}>
      <Typography variant="body2">
        <strong>{group.catalogItemName}</strong> — собрать ОДИН раз, состав скопируется для{" "}
        {pluralCount(group.taskComponents.length, TASKS_GENITIVE)}
      </Typography>
      <BundleTreeForm
        catalogItemId={group.catalogItemId}
        warehouseId={group.warehouseId}
        onChange={onChange}
      />
    </Stack>
  );
}

// после «для» задание встаёт в родительный падеж
const TASKS_GENITIVE = {one: "задания", few: "заданий", many: "заданий"};

// ─── Variation group form ──────────────────────────────────────────────────

interface VariationGroupFormProps {
  group: BatchGroup;
  fulfillment: AddFulfillmentRequest;
  onFulfillmentChange: (f: AddFulfillmentRequest, complete: boolean) => void;
}

function VariationGroupForm({group, fulfillment, onFulfillmentChange}: VariationGroupFormProps) {
  return (
    <Stack spacing={2}>
      <Typography variant="body2">
        <strong>{group.catalogItemName}</strong> — выбрать вариант ОДИН раз для{" "}
        {pluralCount(group.taskComponents.length, TASKS_GENITIVE)}
      </Typography>
      <VariationForm
        catalogItemId={group.catalogItemId}
        warehouseId={group.warehouseId}
        fulfillment={fulfillment}
        onFulfillmentChange={onFulfillmentChange}
      />
    </Stack>
  );
}

// ─── Group row dispatcher ─────────────────────────────────────────────────

interface GroupState {
  standardNode: {nodeId: string | null; nodePath: string | null};
  bundleComponents: AddFulfillmentBundleComponentRequest[];
  variantFulfillment: AddFulfillmentRequest;
  // Whether the bundle tree / variation choice below is fully specified.
  complete: boolean;
}

function emptyGroupState(): GroupState {
  return {
    standardNode: {nodeId: null, nodePath: null},
    bundleComponents: [],
    variantFulfillment: {sourceNodeId: null, quantity: 0},
    complete: false,
  };
}

interface GroupFormRowProps {
  group: BatchGroup;
  state: GroupState;
  onPatch: (key: string, patch: Partial<GroupState>) => void;
}

function GroupFormRow({group, state, onPatch}: GroupFormRowProps) {
  const type = group.catalogItemType;
  const groupKey = group.key;

  // Slot forms below keep these in effect deps, so a fresh identity each render would loop.
  const handleStandardChange = useCallback(
    (standardNode: GroupState["standardNode"]) => onPatch(groupKey, {standardNode}),
    [onPatch, groupKey],
  );
  const handleBundleChange = useCallback(
    (bundleComponents: AddFulfillmentBundleComponentRequest[], complete: boolean) =>
      onPatch(groupKey, {bundleComponents, complete}),
    [onPatch, groupKey],
  );
  const handleVariantChange = useCallback(
    (variantFulfillment: AddFulfillmentRequest, complete: boolean) =>
      onPatch(groupKey, {variantFulfillment, complete}),
    [onPatch, groupKey],
  );

  if (type === "standard") {
    return (
      <StandardGroupForm group={group} value={state.standardNode} onChange={handleStandardChange} />
    );
  }

  if (type === "bundle") {
    return <BundleGroupForm group={group} onChange={handleBundleChange} />;
  }

  if (type === "variation") {
    return (
      <VariationGroupForm
        group={group}
        fulfillment={state.variantFulfillment}
        onFulfillmentChange={handleVariantChange}
      />
    );
  }

  return null;
}

// ─── Main dialog ──────────────────────────────────────────────────────────

interface BatchAssemblyDialogProps {
  open: boolean;
  onClose: () => void;
  selectedTasks: SelectedTaskInfo[];
}

export type {SelectedTaskInfo};

function BatchAssemblyDialog({open, onClose, selectedTasks}: BatchAssemblyDialogProps) {
  // The content is unmounted only after the exit animation; that is what resets the per-group picks.
  const [shownTasks, releaseShownTasks] = useRetainedValue(open ? selectedTasks : null);

  useBackClosable(open, onClose);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownTasks},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownTasks && <BatchAssemblyContent onClose={onClose} selectedTasks={shownTasks} />}
    </Dialog>
  );
}

function BatchAssemblyContent({onClose, selectedTasks}: Omit<BatchAssemblyDialogProps, "open">) {
  const queryClient = useQueryClient();
  const groups = useMemo(() => buildBatchGroups(selectedTasks), [selectedTasks]);

  const [groupStates, setGroupStates] = useState<Map<string, GroupState>>(new Map());

  function getGroupState(key: string): GroupState {
    return groupStates.get(key) ?? emptyGroupState();
  }

  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const submittingRef = useRef(false);

  const mutation = useMutation({
    ...ordersBatchFulfillMutation(),
    meta: {suppressGlobalError: true},
    // Awaited: the mutation stays pending until the refetch lands, so the button cannot be pressed
    // again against stale groups. Invalidated on partial success too, so the refetched tasks shrink
    // the groups to what is still missing and a retry cannot re-send what already went through.
    onSuccess: async (data) => {
      await Promise.all([
        queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()}),
        queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()}),
      ]);
      setFailedItems(data.failedItems);
      if (data.failedItems.length === 0) onClose();
    },
    onError: (error) => setSubmitError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  const patchGroupState = useCallback((key: string, patch: Partial<GroupState>) => {
    setGroupStates((prev) =>
      new Map(prev).set(key, {...(prev.get(key) ?? emptyGroupState()), ...patch}),
    );
  }, []);

  function buildFulfillment(
    group: BatchGroup,
    state: GroupState,
    qty: number,
  ): AddFulfillmentRequest {
    if (group.catalogItemType === "variation") {
      // Unit variants reference a single inventory instance, which is consumed by the first
      // replicated fulfillment — remaining tasks in the group fail.
      return state.variantFulfillment;
    }
    if (group.catalogItemType === "bundle" && state.bundleComponents.length > 0) {
      return {sourceNodeId: null, quantity: 0, bundleComponents: state.bundleComponents};
    }
    return {sourceNodeId: state.standardNode.nodeId, quantity: qty};
  }

  const allGroupsReady = groups.every((group) => {
    const state = getGroupState(group.key);
    return group.catalogItemType === "standard" ? !!state.standardNode.nodeId : state.complete;
  });

  function handleSubmit() {
    if (submittingRef.current) return;
    submittingRef.current = true;

    setFailedItems([]);
    setSubmitError(null);

    const items = groups.flatMap((group) => {
      const state = getGroupState(group.key);
      return group.taskComponents.flatMap((tc) => {
        const fulfillment = buildFulfillment(group, state, tc.qty);
        // Bundle / Unit fulfillments each count as exactly +1 towards task progress (see
        // countFulfilledQty), so a task needing tc.qty of them requires tc.qty separate
        // identical fulfillments — unlike Standard, where quantity is additive.
        const countsAsOne =
          (fulfillment.bundleComponents?.length ?? 0) > 0 || !!fulfillment.unitInventoryItemId;
        const repeat = countsAsOne ? tc.qty : 1;
        return Array.from({length: repeat}, () => ({
          orderId: tc.orderId,
          taskId: tc.taskId,
          taskBoxId: tc.taskBoxId,
          componentId: tc.componentId,
          fulfillment,
        }));
      });
    });

    mutation.mutate({body: {items, autoCompleteTasks: true}});
  }

  return (
    <>
      <DialogTitle>Массовая сборка ({pluralCount(selectedTasks.length, NOUNS.task)})</DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{mt: 1}}>
          {groups.length === 0 && (
            <Typography color="text.secondary">
              В выбранных заданиях не осталось несобранных позиций
            </Typography>
          )}

          {groups.map((group, idx) => {
            const state = getGroupState(group.key);
            return (
              <Stack key={group.key} spacing={1}>
                {idx > 0 && <Divider />}
                <GroupFormRow group={group} state={state} onPatch={patchGroupState} />
              </Stack>
            );
          })}

          {failedItems.length > 0 && (
            <Alert severity="error">
              <Typography variant="body2" sx={{mb: 0.5}}>
                Часть позиций не удалось собрать:
              </Typography>
              {failedItems.map((f, i) => (
                <Typography key={i} variant="caption" sx={{display: "block"}}>
                  • {f.catalogItemName || "Компонент"}: {resolveErrorMessage(f.error)}
                </Typography>
              ))}
            </Alert>
          )}

          {submitError && <Alert severity="error">{submitError}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={mutation.isPending || groups.length === 0 || !allGroupsReady}
        >
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Собрать"}
        </Button>
      </DialogActions>
    </>
  );
}

export default BatchAssemblyDialog;
