import {useMemo, useState} from "react";
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
import {BundleTreeForm, VariationForm} from "./AddFulfillmentDialog";

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
        const key = `${comp.catalogItemId}::${warehouseId}`;
        const existing = groupMap.get(key);
        const entry = {
          orderId,
          taskId,
          taskBoxId: box.id,
          componentId: comp.id,
          qty: comp.quantity,
        };
        if (existing) {
          existing.totalNeeded += comp.quantity;
          existing.taskComponents.push(entry);
        } else {
          groupMap.set(key, {
            key,
            catalogItemId: comp.catalogItemId,
            catalogItemName: comp.catalogItemName,
            catalogItemType: comp.catalogItemType as CatalogItemType,
            warehouseId,
            totalNeeded: comp.quantity,
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
}

function NodePickerRow({label, warehouseId, value, onChange}: NodePickerRowProps) {
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
      <Button size="small" startIcon={<LocationOnIcon />} onClick={() => setOpen(true)}>
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
      />
    </Stack>
  );
}

interface BundleGroupFormProps {
  group: BatchGroup;
  onChange: (v: AddFulfillmentBundleComponentRequest[]) => void;
}

function BundleGroupForm({group, onChange}: BundleGroupFormProps) {
  return (
    <Stack spacing={2}>
      <Typography variant="body2">
        <strong>{group.catalogItemName}</strong> — собрать ОДИН раз, состав скопируется для{" "}
        {group.taskComponents.length} заданий
      </Typography>
      <BundleTreeForm
        catalogItemId={group.catalogItemId}
        warehouseId={group.warehouseId}
        onChange={onChange}
      />
    </Stack>
  );
}

// ─── Variation group form ──────────────────────────────────────────────────

interface VariationGroupFormProps {
  group: BatchGroup;
  state: GroupState;
  onChange: (state: GroupState) => void;
}

function VariationGroupForm({group, state, onChange}: VariationGroupFormProps) {
  return (
    <Stack spacing={2}>
      <Typography variant="body2">
        <strong>{group.catalogItemName}</strong> — выбрать вариант ОДИН раз для{" "}
        {group.taskComponents.length} заданий
      </Typography>
      <VariationForm
        catalogItemId={group.catalogItemId}
        warehouseId={group.warehouseId}
        fulfillment={state.variantFulfillment}
        onFulfillmentChange={(f) => onChange({...state, variantFulfillment: f})}
      />
    </Stack>
  );
}

// ─── Group row dispatcher ─────────────────────────────────────────────────

interface GroupState {
  standardNode: {nodeId: string | null; nodePath: string | null};
  bundleComponents: AddFulfillmentBundleComponentRequest[];
  variantFulfillment: AddFulfillmentRequest;
}

interface GroupFormRowProps {
  group: BatchGroup;
  state: GroupState;
  onChange: (state: GroupState) => void;
}

function GroupFormRow({group, state, onChange}: GroupFormRowProps) {
  const type = group.catalogItemType;

  if (type === "standard") {
    return (
      <StandardGroupForm
        group={group}
        value={state.standardNode}
        onChange={(v) => onChange({...state, standardNode: v})}
      />
    );
  }

  if (type === "bundle") {
    return (
      <BundleGroupForm group={group} onChange={(v) => onChange({...state, bundleComponents: v})} />
    );
  }

  if (type === "variation") {
    return <VariationGroupForm group={group} state={state} onChange={onChange} />;
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
  const queryClient = useQueryClient();
  const groups = useMemo(() => buildBatchGroups(selectedTasks), [selectedTasks]);

  const [groupStates, setGroupStates] = useState<Map<string, GroupState>>(new Map());

  function getGroupState(key: string): GroupState {
    return (
      groupStates.get(key) ?? {
        standardNode: {nodeId: null, nodePath: null},
        bundleComponents: [],
        variantFulfillment: {sourceNodeId: null, quantity: 0},
      }
    );
  }

  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const mutation = useMutation({
    ...ordersBatchFulfillMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      if (data.failedItems.length === 0) {
        queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
        queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
        onClose();
      } else {
        setFailedItems(data.failedItems);
      }
    },
    onError: () => setSubmitError("Ошибка при отправке запроса"),
  });

  function updateGroupState(key: string, state: GroupState) {
    setGroupStates((prev) => new Map(prev).set(key, state));
  }

  function buildFulfillment(
    group: BatchGroup,
    state: GroupState,
    qty: number,
  ): AddFulfillmentRequest {
    if (group.catalogItemType === "variation") {
      // Unit / AssembledBundle variants reference a single inventory instance, which is
      // consumed by the first replicated fulfillment — remaining tasks in the group fail.
      return state.variantFulfillment;
    }
    if (group.catalogItemType === "bundle" && state.bundleComponents.length > 0) {
      return {sourceNodeId: null, quantity: 0, bundleComponents: state.bundleComponents};
    }
    return {sourceNodeId: state.standardNode.nodeId, quantity: qty};
  }

  function handleSubmit() {
    setFailedItems([]);
    setSubmitError(null);

    const items = groups.flatMap((group) => {
      const state = getGroupState(group.key);
      return group.taskComponents.flatMap((tc) => {
        const fulfillment = buildFulfillment(group, state, tc.qty);
        // Bundle / Unit / AssembledBundle fulfillments each count as exactly +1 towards task
        // progress (see countFulfilledQty), so a task needing tc.qty of them requires tc.qty
        // separate identical fulfillments — unlike Standard, where quantity is additive.
        const countsAsOne =
          (fulfillment.bundleComponents?.length ?? 0) > 0 ||
          !!fulfillment.unitInventoryItemId ||
          !!fulfillment.assembledBundleInventoryItemId;
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

    mutation.mutate({body: {items}});
  }

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Массовая сборка ({selectedTasks.length} заданий)</DialogTitle>
      <DialogContent>
        <Stack spacing={3} sx={{mt: 1}}>
          {groups.map((group, idx) => {
            const state = getGroupState(group.key);
            return (
              <Stack key={group.key} spacing={1}>
                {idx > 0 && <Divider />}
                <GroupFormRow
                  group={group}
                  state={state}
                  onChange={(s) => updateGroupState(group.key, s)}
                />
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
                  • Компонент {f.componentId.slice(0, 8)}: {f.error}
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
        <Button variant="contained" onClick={handleSubmit} disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Собрать"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default BatchAssemblyDialog;
