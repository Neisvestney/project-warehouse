import {useCallback, useRef, useState} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  LinearProgress,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  ordersAddFulfillmentMutation,
  ordersBatchFulfillMutation,
  ordersGetAllAssemblyQueryKey,
  ordersGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {
  AddFulfillmentBundleComponentRequest,
  AddFulfillmentRequest,
  AssemblyTaskBoxComponentDto,
  BatchFulfillFailedItem,
  UnitInventoryItemDto,
} from "@/api/types.gen";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {countFulfilledQty} from "@/components/orders/orderAssemblyUtils";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useDefaultStorageNode} from "@/hooks/useDefaultStorageNode";
import {useNodeItemCount} from "@/hooks/useNodeItemCount";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {ComponentRow, TodoRegistryProvider, UnfilledCounter} from "./ComponentRow";
import {EMPTY_STATUS, isComplete, isShort, type SlotStatus} from "./fulfillmentStatus";
import {useTodoRegistry} from "./todoRegistry";
import {NodeControl, UnitPicker, type NodePick} from "./FulfillmentControls";
import {BundleTree} from "./FulfillmentTree";
import {VariationChain} from "./VariationChain";
import {chainLeaf, type VariantStep} from "./variationOptions";

interface AddFulfillmentDialogProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  warehouseId: string;
  taskId: string;
  taskBoxId: string;
  component: AssemblyTaskBoxComponentDto;
  boxLabel?: string;
}

function AddFulfillmentDialog({open, ...props}: AddFulfillmentDialogProps) {
  const theme = useTheme();
  const fullScreen = useMediaQuery(theme.breakpoints.down("sm"));
  // The content is unmounted only after the exit animation; that is what resets the built fulfillment.
  const [shownOpen, releaseShown] = useRetainedValue(open || null);

  useBackClosable(open, props.onClose);

  return (
    <Dialog
      open={open}
      onClose={props.onClose}
      maxWidth="sm"
      fullWidth
      fullScreen={fullScreen}
      slotProps={{
        transition: {onExited: releaseShown},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownOpen && <AddFulfillmentContent {...props} />}
    </Dialog>
  );
}

function AddFulfillmentContent({
  onClose,
  orderId,
  warehouseId,
  taskId,
  taskBoxId,
  component,
  boxLabel,
}: Omit<AddFulfillmentDialogProps, "open">) {
  const queryClient = useQueryClient();
  const {registry, scrollToFirst} = useTodoRegistry();

  const [chain, setChain] = useState<VariantStep[]>([]);
  const [override, setOverride] = useState<NodePick | null>(null);
  const [unitItem, setUnitItem] = useState<UnitInventoryItemDto | null>(null);
  const [bundleEntries, setBundleEntries] = useState<AddFulfillmentBundleComponentRequest[]>([]);
  const [bundleStatus, setBundleStatus] = useState<SlotStatus>(EMPTY_STATUS);
  const [error, setError] = useState<string | null>(null);
  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);
  const submittingRef = useRef(false);

  const isVariation = component.catalogItemType === "variation";
  const leaf = chainLeaf(chain);
  const effectiveType = isVariation ? (leaf?.type ?? null) : component.catalogItemType;
  const effectiveItemId = isVariation ? (leaf?.id ?? null) : component.catalogItemId;

  const fulfilled = countFulfilledQty(component.fulfillments);
  const remaining = Math.max(1, component.quantity - fulfilled);
  const [quantity, setQuantity] = useState(remaining);

  const defaultNode = useDefaultStorageNode(warehouseId, effectiveType === "standard");
  const node =
    override ??
    (defaultNode
      ? {nodeId: defaultNode.nodeId, nodePath: formatStoragePlaceNodeName(defaultNode.nodePath)}
      : null);

  const available = useNodeItemCount(node?.nodeId, effectiveItemId);
  const nodeReady = !!node && !isShort(available, quantity);

  const handleBundleChange = useCallback(
    (entries: AddFulfillmentBundleComponentRequest[], status: SlotStatus) => {
      setBundleEntries(entries);
      setBundleStatus(status);
    },
    [],
  );

  const mutation = useMutation({
    ...ordersAddFulfillmentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: (e) => setError(extractErrorMessage(e)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  const batchMutation = useMutation({
    ...ordersBatchFulfillMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      if (data.failedItems.length === 0) onClose();
      else setFailedItems(data.failedItems);
    },
    onError: (e) => setError(extractErrorMessage(e)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  // Bundle and Unit fulfillments count as one piece each, so quantity is the number of copies sent.
  const countsAsOne = effectiveType === "bundle" || effectiveType === "unit";
  const addingNow = effectiveType === "unit" ? 1 : quantity;

  const canSubmit =
    (!isVariation || !!leaf) &&
    (effectiveType === "standard"
      ? nodeReady && quantity > 0
      : effectiveType === "unit"
        ? !!unitItem
        : effectiveType === "bundle"
          ? isComplete(bundleStatus)
          : false);

  function buildRequest(): AddFulfillmentRequest {
    const resolved = isVariation ? {resolvedCatalogItemId: leaf?.id ?? null} : {};
    if (effectiveType === "unit" && unitItem) {
      return {
        sourceNodeId: unitItem.nodeId,
        quantity: 0,
        unitInventoryItemId: unitItem.id,
        ...resolved,
      };
    }
    if (effectiveType === "bundle") {
      return {sourceNodeId: null, quantity: 0, bundleComponents: bundleEntries, ...resolved};
    }
    return {sourceNodeId: node?.nodeId ?? null, quantity, ...resolved};
  }

  function handleSubmit() {
    if (submittingRef.current || !canSubmit) return;
    submittingRef.current = true;
    setError(null);
    setFailedItems([]);

    const fulfillment = buildRequest();

    if (countsAsOne && addingNow > 1) {
      const items = Array.from({length: addingNow}, () => ({
        orderId,
        taskId,
        taskBoxId,
        componentId: component.id,
        fulfillment,
      }));
      batchMutation.mutate({
        body: {items, autoCompleteTasks: false, allowPartialSuccess: false},
      });
      return;
    }

    mutation.mutate({
      path: {id: orderId, taskId, tbid: taskBoxId, cid: component.id},
      body: fulfillment,
    });
  }

  const isPending = mutation.isPending || batchMutation.isPending;
  const leftAfter = component.quantity - fulfilled - addingNow;

  return (
    <>
      <DialogTitle sx={{pb: 1}}>
        <Stack spacing={0.75}>
          <Typography variant="h6">{component.catalogItemName}</Typography>
          {boxLabel && (
            <Typography variant="caption" color="text.secondary">
              {boxLabel}
            </Typography>
          )}
          <Stack direction="row" spacing={1.5} sx={{alignItems: "center"}}>
            <Typography variant="caption" sx={{whiteSpace: "nowrap"}}>
              собрано {fulfilled} из {component.quantity}
            </Typography>
            {/* Intent, not validity: tying the buffer to canSubmit makes the bar flap while the
                composition tree and the cell stock are still loading. */}
            <LinearProgress
              variant="buffer"
              value={(fulfilled / component.quantity) * 100}
              valueBuffer={((fulfilled + addingNow) / component.quantity) * 100}
              sx={{
                flexGrow: 1,
                height: 6,
                borderRadius: 3,
                // The dashed tail animates and the bars ease between values; both read as flicker here.
                "& .MuiLinearProgress-dashed": {animation: "none", backgroundImage: "none"},
                "& .MuiLinearProgress-bar": {transition: "none"},
              }}
            />
            <Typography variant="caption" color="text.secondary" sx={{whiteSpace: "nowrap"}}>
              добавляем {addingNow}
            </Typography>
          </Stack>
        </Stack>
      </DialogTitle>

      <DialogContent>
        <TodoRegistryProvider registry={registry}>
          <Stack spacing={1.5} sx={{mt: 1}}>
            {isVariation && (
              <VariationChain
                rootCatalogItemId={component.catalogItemId}
                chain={chain}
                onChange={(next) => {
                  setChain(next);
                  setOverride(null);
                  setUnitItem(null);
                  setBundleEntries([]);
                  setBundleStatus(EMPTY_STATUS);
                }}
              />
            )}

            {effectiveType === "standard" && effectiveItemId && (
              <ComponentRow
                name={component.catalogItemName}
                catalogItemType="standard"
                status={{filled: nodeReady ? 1 : 0, total: 1}}
                summary={
                  <NodeControl
                    warehouseId={warehouseId}
                    catalogItemId={effectiveItemId}
                    node={node}
                    isDefault={!override && !!defaultNode}
                    available={available}
                    needQty={quantity}
                    onSelect={setOverride}
                  />
                }
              />
            )}

            {effectiveType === "unit" && effectiveItemId && (
              <ComponentRow
                name={component.catalogItemName}
                catalogItemType="unit"
                status={{filled: unitItem ? 1 : 0, total: 1}}
                active={!unitItem}
              >
                <UnitPicker
                  catalogItemId={effectiveItemId}
                  warehouseId={warehouseId}
                  value={unitItem}
                  onChange={setUnitItem}
                />
              </ComponentRow>
            )}

            {effectiveType === "bundle" && effectiveItemId && (
              <BundleTree
                key={effectiveItemId}
                catalogItemId={effectiveItemId}
                warehouseId={warehouseId}
                needTimes={addingNow}
                onChange={handleBundleChange}
              />
            )}

            {effectiveType && effectiveType !== "unit" && (
              <QuantityField
                label={effectiveType === "bundle" ? "Комплектов сейчас" : "Количество"}
                value={quantity}
                max={remaining}
                onChange={setQuantity}
              />
            )}

            {error && <Alert severity="error">{error}</Alert>}
            {failedItems.length > 0 && (
              <Alert severity="error">
                <Typography variant="body2" sx={{mb: 0.5}}>
                  Удалось собрать {addingNow - failedItems.length} из {addingNow}:
                </Typography>
                {failedItems.map((f, i) => (
                  <Typography key={i} variant="caption" sx={{display: "block"}}>
                    • {resolveErrorMessage(f.error)}
                  </Typography>
                ))}
              </Alert>
            )}
          </Stack>
        </TodoRegistryProvider>
      </DialogContent>

      <DialogActions sx={{gap: 1}}>
        {effectiveType === "bundle" && !isComplete(bundleStatus) ? (
          <UnfilledCounter status={bundleStatus} onJump={scrollToFirst} />
        ) : (
          canSubmit &&
          leftAfter > 0 && (
            <Typography variant="caption" color="text.secondary">
              останется {leftAfter}
            </Typography>
          )
        )}
        <Stack direction="row" spacing={1} sx={{ml: "auto"}}>
          <Button onClick={onClose} disabled={isPending}>
            Отмена
          </Button>
          <Button variant="contained" onClick={handleSubmit} disabled={isPending || !canSubmit}>
            {isPending ? (
              <CircularProgress size={20} color="inherit" />
            ) : remaining > 1 ? (
              `Добавить ${addingNow} из ${remaining}`
            ) : (
              "Добавить"
            )}
          </Button>
        </Stack>
      </DialogActions>
    </>
  );
}

interface QuantityFieldProps {
  label: string;
  value: number;
  max: number;
  onChange: (value: number) => void;
}

/** Partial assembly is normal: take what the cell holds now, come back for the rest. */
function QuantityField({label, value, max, onChange}: QuantityFieldProps) {
  return (
    <Stack spacing={0.5}>
      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
        <ClampedIntegerField
          label={label}
          size="small"
          value={value}
          max={max}
          onCommit={onChange}
          sx={{width: 160}}
        />
        {max > 1 && value !== max && (
          <Button size="small" variant="outlined" onClick={() => onChange(max)}>
            Все {max}
          </Button>
        )}
      </Stack>
      {max > 1 && value < max && (
        <Typography variant="caption" color="text.secondary">
          Остальное можно добавить позже — другим заходом, из другой ячейки.
        </Typography>
      )}
    </Stack>
  );
}

export type {AddFulfillmentDialogProps};
export default AddFulfillmentDialog;
