import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {
  Alert,
  Autocomplete,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {useMutation, useQueries, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  catalogGetByIdOptions,
  inventoryItemsGetAllUnitsOptions,
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
  CatalogItemType,
  UnitInventoryItemDto,
} from "@/api/types.gen";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import {countFulfilledQty} from "@/components/orders/orderAssemblyUtils";
import SelectNodeModal, {type SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDebounce} from "@/hooks/useDebounce";
import {useDefaultStorageNode} from "@/hooks/useDefaultStorageNode";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {pluralCount} from "@/utils/pluralUtils";
import {useRetainedValue} from "@/hooks/useRetainedValue";

// ─── Node field (display + pick, no manual typing) ─────────────────────────

interface NodeFieldProps {
  label: string;
  warehouseId: string;
  nodePath: string | null;
  onSelect: (node: SelectedNode) => void;
  onClear?: () => void;
  catalogItemId?: string;
}

function NodeField({
  label,
  warehouseId,
  nodePath,
  onSelect,
  onClear,
  catalogItemId,
}: NodeFieldProps) {
  const [open, setOpen] = useState(false);

  return (
    <Stack spacing={0.5}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
        <Typography
          variant="body2"
          sx={{
            flexGrow: 1,
            color: nodePath ? "text.primary" : "text.disabled",
            fontStyle: nodePath ? "normal" : "italic",
          }}
        >
          {nodePath ?? "Не выбрано"}
        </Typography>
        <Button
          variant="outlined"
          size="small"
          startIcon={<LocationOnIcon />}
          onClick={() => setOpen(true)}
        >
          Выбрать
        </Button>
        {onClear && nodePath && (
          <Button size="small" onClick={onClear}>
            Сбросить
          </Button>
        )}
      </Stack>
      <SelectNodeModal
        open={open}
        onClose={() => setOpen(false)}
        warehouseId={warehouseId}
        onSelect={(node) => {
          onSelect(node);
          setOpen(false);
        }}
        catalogItemId={catalogItemId}
      />
    </Stack>
  );
}

interface AddFulfillmentDialogProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  warehouseId: string;
  taskId: string;
  taskBoxId: string;
  component: AssemblyTaskBoxComponentDto;
}

// ─── Standard fulfillment ──────────────────────────────────────────────────

interface StandardFormProps {
  warehouseId: string;
  maxQuantity?: number;
  value: {sourceNodeId: string | null; nodePath: string | null; quantity: number};
  onChange: (v: {sourceNodeId: string | null; nodePath: string | null; quantity: number}) => void;
  catalogItemId?: string;
}

function StandardForm({
  warehouseId,
  maxQuantity,
  value,
  onChange,
  catalogItemId,
}: StandardFormProps) {
  return (
    <Stack spacing={2}>
      <NodeField
        label="Ячейка источника"
        warehouseId={warehouseId}
        nodePath={value.nodePath}
        onSelect={(node) =>
          onChange({
            ...value,
            sourceNodeId: node.nodeId,
            nodePath: formatStoragePlaceNodeName(node.nodePath),
          })
        }
        catalogItemId={catalogItemId}
      />
      <ClampedIntegerField
        label="Количество"
        size="small"
        value={value.quantity}
        max={maxQuantity}
        onCommit={(quantity) => onChange({...value, quantity})}
      />
    </Stack>
  );
}

// ─── Unit fulfillment ──────────────────────────────────────────────────────

interface UnitFormProps {
  catalogItemId: string;
  warehouseId: string;
  value: string | null;
  onChange: (id: string | null, nodeId: string | null) => void;
}

function UnitForm({catalogItemId, warehouseId, onChange}: UnitFormProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);
  const [nodeFilter, setNodeFilter] = useState<{id: string; path: string} | null>(null);
  const [selectedItem, setSelectedItem] = useState<UnitInventoryItemDto | null>(null);

  const query = useQuery({
    ...inventoryItemsGetAllUnitsOptions({
      query: {
        catalogItemId,
        warehouseId,
        pageSize: 50,
        searchString: debouncedInput || undefined,
        nodeId: nodeFilter?.id,
      },
    }),
  });

  const options = useMemo(() => {
    const results = query.data?.items ?? [];
    if (selectedItem && !results.some((i) => i.id === selectedItem.id)) {
      return [selectedItem, ...results];
    }
    return results;
  }, [query.data, selectedItem]);

  return (
    <Stack spacing={1}>
      <Autocomplete
        size="small"
        options={options}
        value={selectedItem}
        onChange={(_, item) => {
          setSelectedItem(item);
          onChange(item?.id ?? null, item?.nodeId ?? null);
        }}
        inputValue={inputValue}
        onInputChange={(_, v) => setInputValue(v)}
        getOptionLabel={(item) =>
          `#${item.inventoryNumber} — ${item.storagePlaceName} / ${item.nodeName}`
        }
        isOptionEqualToValue={(o, v) => o.id === v.id}
        filterOptions={(x) => x}
        loading={query.isLoading}
        noOptionsText="Нет доступных экземпляров"
        renderInput={(params) => <TextField {...params} label="Экземпляр (поиск по номеру)" />}
      />
      <NodeField
        label="Фильтр по ячейке"
        warehouseId={warehouseId}
        nodePath={nodeFilter?.path ?? null}
        onSelect={(node) =>
          setNodeFilter({id: node.nodeId, path: formatStoragePlaceNodeName(node.nodePath)})
        }
        onClear={() => setNodeFilter(null)}
        catalogItemId={catalogItemId}
      />
    </Stack>
  );
}

// ─── Bundle slot (recursive — handles standard/unit/nested bundle/variation) ─

interface BundleSlotFormProps {
  warehouseId: string;
  slotKey: string;
  catalogItemId: string;
  catalogItemType: CatalogItemType;
  componentName: string;
  multiplier: number;
  onChange: (
    slotKey: string,
    entries: AddFulfillmentBundleComponentRequest[],
    complete: boolean,
  ) => void;
}

function BundleSlotForm({
  warehouseId,
  slotKey,
  catalogItemId,
  catalogItemType,
  componentName,
  multiplier,
  onChange,
}: BundleSlotFormProps) {
  const [overrideNode, setOverrideNode] = useState<SelectedNode | null>(null);
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null);
  const [selectedVariantType, setSelectedVariantType] = useState<CatalogItemType | null>(null);

  const variationQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: catalogItemId}}),
    enabled: catalogItemType === "variation",
  });
  const memberIds = variationQuery.data?.memberIds ?? [];
  const memberQueries = useQueries({
    queries: memberIds.map((id) => catalogGetByIdOptions({path: {id}})),
  });

  const defaultNode = useDefaultStorageNode(warehouseId, catalogItemType === "standard");
  const effectiveNode = overrideNode ?? defaultNode;

  useEffect(() => {
    if (catalogItemType === "standard" && effectiveNode) {
      onChange(
        slotKey,
        [
          {
            catalogItemId,
            sourceNodeId: effectiveNode.nodeId,
            quantity: multiplier,
            unitInventoryItemId: null,
          },
        ],
        true,
      );
    }
  }, [catalogItemType, effectiveNode, catalogItemId, multiplier, slotKey, onChange]);

  // A slot awaiting input must announce itself, otherwise the parent never learns it exists.
  useEffect(() => {
    if (catalogItemType === "unit" || (catalogItemType === "variation" && !selectedVariantId)) {
      onChange(slotKey, [], false);
    }
  }, [catalogItemType, selectedVariantId, slotKey, onChange]);

  const handleNestedChange = useCallback(
    (entries: AddFulfillmentBundleComponentRequest[], complete: boolean) =>
      onChange(slotKey, entries, complete),
    [onChange, slotKey],
  );

  if (catalogItemType === "standard") {
    return (
      <NodeField
        label={`${componentName} × ${multiplier}`}
        warehouseId={warehouseId}
        nodePath={effectiveNode ? formatStoragePlaceNodeName(effectiveNode.nodePath) : null}
        onSelect={(node) => setOverrideNode(node)}
        catalogItemId={catalogItemId}
      />
    );
  }

  if (catalogItemType === "unit") {
    return (
      <Stack spacing={0.5}>
        <Typography variant="caption" color="text.secondary">
          {componentName}
        </Typography>
        <UnitForm
          catalogItemId={catalogItemId}
          warehouseId={warehouseId}
          value={null}
          onChange={(id, nodeId) =>
            onChange(
              slotKey,
              id
                ? [
                    {
                      catalogItemId,
                      sourceNodeId: nodeId ?? "",
                      quantity: 0,
                      unitInventoryItemId: id,
                    },
                  ]
                : [],
              !!id,
            )
          }
        />
      </Stack>
    );
  }

  if (catalogItemType === "bundle") {
    return (
      <Stack spacing={1} sx={{pl: 1, borderLeft: "2px solid", borderColor: "divider"}}>
        <Typography variant="caption" color="text.secondary">
          {componentName} × {multiplier} (вложенный комплект)
        </Typography>
        <BundleTreeForm
          catalogItemId={catalogItemId}
          warehouseId={warehouseId}
          multiplier={multiplier}
          onChange={handleNestedChange}
        />
      </Stack>
    );
  }

  if (catalogItemType === "variation") {
    if (variationQuery.isLoading || memberQueries.some((q) => q.isLoading)) {
      return <CircularProgress size={20} />;
    }

    const variantOptions = memberQueries
      .map((q) => q.data)
      .filter((d): d is NonNullable<typeof d> => !!d);

    return (
      <Stack spacing={1} sx={{pl: 1, borderLeft: "2px solid", borderColor: "divider"}}>
        <FormControl size="small" fullWidth error={!selectedVariantId}>
          <InputLabel>{componentName} — вариант</InputLabel>
          <Select
            value={selectedVariantId ?? ""}
            label={`${componentName} — вариант`}
            onChange={(e) => {
              const variant = variantOptions.find((v) => v.id === e.target.value);
              setSelectedVariantId(e.target.value || null);
              setSelectedVariantType(variant?.type ?? null);
              onChange(slotKey, [], false);
            }}
          >
            {variantOptions.map((v) => (
              <MenuItem key={v.id} value={v.id}>
                {v.fullName}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        {selectedVariantId && selectedVariantType && (
          <BundleSlotForm
            key={selectedVariantId}
            warehouseId={warehouseId}
            slotKey={slotKey}
            catalogItemId={selectedVariantId}
            catalogItemType={selectedVariantType}
            componentName={componentName}
            multiplier={multiplier}
            onChange={onChange}
          />
        )}
      </Stack>
    );
  }

  return (
    <Alert severity="warning">
      Тип компонента "{componentName}" не поддерживается в составе комплекта
    </Alert>
  );
}

// ─── Bundle tree fulfillment (mode 1, recursive) ───────────────────────────

interface BundleTreeFormProps {
  catalogItemId: string;
  warehouseId: string;
  multiplier?: number;
  onChange: (entries: AddFulfillmentBundleComponentRequest[], complete: boolean) => void;
}

function BundleTreeForm({
  catalogItemId,
  warehouseId,
  multiplier = 1,
  onChange,
}: BundleTreeFormProps) {
  const catalogQuery = useQuery(catalogGetByIdOptions({path: {id: catalogItemId}}));
  const catalogComponents = catalogQuery.data?.components;
  const components = useMemo(() => catalogComponents ?? [], [catalogComponents]);
  const slotEntriesRef = useRef<Map<string, AddFulfillmentBundleComponentRequest[]>>(new Map());
  const slotCompleteRef = useRef<Map<string, boolean>>(new Map());
  const slotKeys = useMemo(() => components.map((c) => c.componentId), [components]);

  const updateSlot = useCallback(
    (slotKey: string, entries: AddFulfillmentBundleComponentRequest[], complete: boolean) => {
      slotEntriesRef.current.set(slotKey, entries);
      slotCompleteRef.current.set(slotKey, complete);
      const allComplete =
        slotKeys.length > 0 && slotKeys.every((key) => slotCompleteRef.current.get(key) === true);
      onChange(Array.from(slotEntriesRef.current.values()).flat(), allComplete);
    },
    [onChange, slotKeys],
  );

  if (catalogQuery.isLoading) return <CircularProgress size={20} />;

  return (
    <Stack spacing={2} divider={<Divider orientation="horizontal" flexItem />}>
      {components.map((comp) => (
        <BundleSlotForm
          key={comp.componentId}
          warehouseId={warehouseId}
          slotKey={comp.componentId}
          catalogItemId={comp.componentId}
          catalogItemType={comp.componentType}
          componentName={comp.componentName}
          multiplier={comp.quantity * multiplier}
          onChange={updateSlot}
        />
      ))}
    </Stack>
  );
}

// ─── Variation fulfillment ────────────────────────────────────────────────

interface VariationFormProps {
  catalogItemId: string;
  warehouseId: string;
  maxQuantity?: number;
  fulfillment: AddFulfillmentRequest;
  onFulfillmentChange: (f: AddFulfillmentRequest, complete: boolean) => void;
}

function VariationForm({
  catalogItemId,
  warehouseId,
  maxQuantity,
  fulfillment,
  onFulfillmentChange,
}: VariationFormProps) {
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null);
  const [selectedVariantType, setSelectedVariantType] = useState<CatalogItemType | null>(null);

  // The leaf variant is what inventory moves against — a nested VariationForm passes its own
  // choice straight through, overwriting ours.
  const handleLeafChange = useCallback(
    (f: AddFulfillmentRequest, complete: boolean) =>
      onFulfillmentChange({...f, resolvedCatalogItemId: selectedVariantId}, complete),
    [onFulfillmentChange, selectedVariantId],
  );

  const catalogQuery = useQuery(catalogGetByIdOptions({path: {id: catalogItemId}}));
  const memberIds = catalogQuery.data?.memberIds ?? [];
  const memberQueries = useQueries({
    queries: memberIds.map((id) => catalogGetByIdOptions({path: {id}})),
  });

  if (catalogQuery.isLoading || memberQueries.some((q) => q.isLoading)) {
    return <CircularProgress size={20} />;
  }

  const variantOptions = memberQueries
    .map((q) => q.data)
    .filter((d): d is NonNullable<typeof d> => !!d);

  return (
    <Stack spacing={2}>
      <FormControl size="small" fullWidth error={!selectedVariantId}>
        <InputLabel>Вариант</InputLabel>
        <Select
          value={selectedVariantId ?? ""}
          onChange={(e) => {
            const variant = variantOptions.find((v) => v.id === e.target.value);
            setSelectedVariantId(e.target.value || null);
            setSelectedVariantType(variant?.type ?? null);
            onFulfillmentChange(
              {sourceNodeId: null, quantity: 1, resolvedCatalogItemId: e.target.value || null},
              false,
            );
          }}
          label="Вариант"
        >
          {variantOptions.map((v) => (
            <MenuItem key={v.id} value={v.id}>
              {v.fullName}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {selectedVariantId &&
        selectedVariantType &&
        (selectedVariantType === "variation" ? (
          <>
            <Divider />
            <VariationForm
              key={selectedVariantId}
              catalogItemId={selectedVariantId}
              warehouseId={warehouseId}
              maxQuantity={maxQuantity}
              fulfillment={fulfillment}
              onFulfillmentChange={onFulfillmentChange}
            />
          </>
        ) : (
          <>
            <Divider />
            <SubFulfillmentForm
              key={selectedVariantId}
              catalogItemId={selectedVariantId}
              catalogItemType={selectedVariantType}
              warehouseId={warehouseId}
              maxQuantity={maxQuantity}
              fulfillment={fulfillment}
              onChange={handleLeafChange}
            />
          </>
        ))}
    </Stack>
  );
}

// ─── Sub-fulfillment form dispatcher ────────────────────────────────────────

interface SubFulfillmentFormProps {
  catalogItemId: string;
  catalogItemType: CatalogItemType;
  warehouseId: string;
  maxQuantity?: number;
  fulfillment: AddFulfillmentRequest;
  onChange: (f: AddFulfillmentRequest, complete: boolean) => void;
}

function SubFulfillmentForm({
  catalogItemId,
  catalogItemType,
  warehouseId,
  maxQuantity,
  fulfillment,
  onChange,
}: SubFulfillmentFormProps) {
  const [overrideNode, setOverrideNode] = useState<{
    sourceNodeId: string;
    nodePath: string;
  } | null>(null);
  const [quantity, setQuantity] = useState(() =>
    Math.min(Math.max(1, fulfillment.quantity), maxQuantity ?? Number.MAX_SAFE_INTEGER),
  );

  const defaultNode = useDefaultStorageNode(warehouseId, catalogItemType === "standard");
  const nodeState = {
    sourceNodeId: overrideNode?.sourceNodeId ?? defaultNode?.nodeId ?? null,
    nodePath:
      overrideNode?.nodePath ??
      (defaultNode ? formatStoragePlaceNodeName(defaultNode.nodePath) : null),
    quantity,
  };

  useEffect(() => {
    if (catalogItemType === "standard" && nodeState.sourceNodeId) {
      onChange(
        {sourceNodeId: nodeState.sourceNodeId, quantity: nodeState.quantity},
        nodeState.quantity > 0,
      );
    }
  }, [catalogItemType, nodeState.sourceNodeId, nodeState.quantity, onChange]);

  // Unit and bundle sub-forms stay silent until filled in, so seed an incomplete state.
  useEffect(() => {
    if (catalogItemType === "unit") {
      onChange({sourceNodeId: null, quantity: 0}, false);
    }
  }, [catalogItemType, onChange]);

  const handleBundleChange = useCallback(
    (comps: AddFulfillmentBundleComponentRequest[], complete: boolean) =>
      onChange({sourceNodeId: null, quantity: 0, bundleComponents: comps}, complete),
    [onChange],
  );

  if (catalogItemType === "standard") {
    return (
      <StandardForm
        warehouseId={warehouseId}
        maxQuantity={maxQuantity}
        value={nodeState}
        onChange={(v) => {
          if (v.sourceNodeId && v.nodePath) {
            setOverrideNode({sourceNodeId: v.sourceNodeId, nodePath: v.nodePath});
          }
          setQuantity(v.quantity);
        }}
        catalogItemId={catalogItemId}
      />
    );
  }

  if (catalogItemType === "unit") {
    return (
      <UnitForm
        catalogItemId={catalogItemId}
        warehouseId={warehouseId}
        value={fulfillment.unitInventoryItemId ?? null}
        onChange={(id, nodeId) =>
          onChange({sourceNodeId: nodeId, quantity: 0, unitInventoryItemId: id}, !!id)
        }
      />
    );
  }

  if (catalogItemType === "bundle") {
    return (
      <BundleTreeForm
        key={catalogItemId}
        catalogItemId={catalogItemId}
        warehouseId={warehouseId}
        onChange={handleBundleChange}
      />
    );
  }

  return null;
}

// ─── Main dialog ──────────────────────────────────────────────────────────

function AddFulfillmentDialog({open, ...props}: AddFulfillmentDialogProps) {
  // The content is unmounted only after the exit animation; that is what resets the built fulfillment.
  const [shownOpen, releaseShown] = useRetainedValue(open || null);

  useBackClosable(open, props.onClose);

  return (
    <Dialog
      open={open}
      onClose={props.onClose}
      maxWidth="sm"
      fullWidth
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
}: Omit<AddFulfillmentDialogProps, "open">) {
  const queryClient = useQueryClient();
  const [fulfillment, setFulfillment] = useState<AddFulfillmentRequest>({
    sourceNodeId: null,
    quantity: component.quantity,
  });
  const [isComplete, setIsComplete] = useState(false);
  const [bundleCount, setBundleCount] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const handleFulfillmentChange = useCallback((f: AddFulfillmentRequest, complete: boolean) => {
    setFulfillment(f);
    setIsComplete(complete);
  }, []);
  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);
  const submittingRef = useRef(false);

  const mutation = useMutation({
    ...ordersAddFulfillmentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: (error) => setError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  const batchMutation = useMutation({
    ...ordersBatchFulfillMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      if (data.failedItems.length === 0) {
        queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
        queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
        onClose();
      } else {
        setFailedItems(data.failedItems);
      }
    },
    onError: (error) => setError(extractErrorMessage(error)),
    onSettled: () => {
      submittingRef.current = false;
    },
  });

  const isVariation = component.catalogItemType === "variation";
  const isBundleFulfillment = (fulfillment.bundleComponents?.length ?? 0) > 0;
  const remaining = component.quantity - countFulfilledQty(component.fulfillments);
  const maxQuantity = Math.max(1, remaining);

  function handleSubmit() {
    if (submittingRef.current) return;
    submittingRef.current = true;

    setError(null);
    setFailedItems([]);
    if (isBundleFulfillment && bundleCount > 1) {
      const items = Array.from({length: bundleCount}, () => ({
        orderId,
        taskId,
        taskBoxId,
        componentId: component.id,
        fulfillment,
      }));
      batchMutation.mutate({body: {items, autoCompleteTasks: false}});
    } else {
      mutation.mutate({
        path: {id: orderId, taskId, tbid: taskBoxId, cid: component.id},
        body: fulfillment,
      });
    }
  }

  // Every slot of the tree has to report itself filled — a half-built bundle would otherwise
  // count as a whole one while only deducting the slots that were filled.
  const canSubmit = isComplete && (!isVariation || !!fulfillment.resolvedCatalogItemId);

  const isPending = mutation.isPending || batchMutation.isPending;

  return (
    <>
      <DialogTitle>Добавить фулфилмент — {component.catalogItemName}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          {isVariation ? (
            <VariationForm
              catalogItemId={component.catalogItemId}
              warehouseId={warehouseId}
              maxQuantity={maxQuantity}
              fulfillment={fulfillment}
              onFulfillmentChange={handleFulfillmentChange}
            />
          ) : (
            <SubFulfillmentForm
              catalogItemId={component.catalogItemId}
              catalogItemType={component.catalogItemType}
              warehouseId={warehouseId}
              maxQuantity={maxQuantity}
              fulfillment={fulfillment}
              onChange={handleFulfillmentChange}
            />
          )}

          {!canSubmit && (
            <Typography variant="caption" color="text.secondary">
              Заполните все позиции — незаполненные подсвечены.
            </Typography>
          )}

          {isBundleFulfillment && (
            <ClampedIntegerField
              label="Количество комплектов для сборки"
              size="small"
              value={bundleCount}
              max={maxQuantity}
              onCommit={setBundleCount}
              helperText={
                bundleCount > 1
                  ? `Будет создано ${pluralCount(bundleCount, {
                      one: "одинаковый фулфилмент",
                      few: "одинаковых фулфилмента",
                      many: "одинаковых фулфилментов",
                    })} комплекта`
                  : undefined
              }
            />
          )}

          {error && <Alert severity="error">{error}</Alert>}
          {failedItems.length > 0 && (
            <Alert severity="error">
              <Typography variant="body2" sx={{mb: 0.5}}>
                Удалось собрать {bundleCount - failedItems.length} из {bundleCount}:
              </Typography>
              {failedItems.map((f, i) => (
                <Typography key={i} variant="caption" sx={{display: "block"}}>
                  • {resolveErrorMessage(f.error)}
                </Typography>
              ))}
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={isPending || !canSubmit}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Добавить"}
        </Button>
      </DialogActions>
    </>
  );
}

export {UnitForm, BundleTreeForm, VariationForm};
export default AddFulfillmentDialog;
