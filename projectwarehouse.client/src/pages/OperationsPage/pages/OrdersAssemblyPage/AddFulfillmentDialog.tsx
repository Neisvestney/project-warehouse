import {useMemo, useRef, useState} from "react";
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
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import LocationOnIcon from "@mui/icons-material/LocationOn";
import {useMutation, useQueries, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  catalogGetByIdOptions,
  inventoryItemsGetAllAssembledBundlesOptions,
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
import SelectNodeModal, {type SelectedNode} from "@/components/receipts/SelectNodeModal";
import {countFulfilledQty} from "@/components/orders/orderAssemblyUtils";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {useDebounce} from "@/hooks/useDebounce";

// ─── Node field (display + pick, no manual typing) ─────────────────────────

interface NodeFieldProps {
  label: string;
  warehouseId: string;
  nodePath: string | null;
  onSelect: (node: SelectedNode) => void;
  onClear?: () => void;
}

function NodeField({label, warehouseId, nodePath, onSelect, onClear}: NodeFieldProps) {
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
  quantity: number;
  value: {sourceNodeId: string | null; nodePath: string | null; quantity: number};
  onChange: (v: {sourceNodeId: string | null; nodePath: string | null; quantity: number}) => void;
}

function StandardForm({warehouseId, value, onChange}: StandardFormProps) {
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
      />
      <TextField
        label="Количество"
        type="number"
        size="small"
        value={value.quantity}
        onChange={(e) => onChange({...value, quantity: Math.max(1, Number(e.target.value))})}
        slotProps={{htmlInput: {min: 1}}}
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
      />
    </Stack>
  );
}

// ─── AssembledBundle fulfillment ────────────────────────────────────────────

interface AssembledBundleFormProps {
  catalogItemId: string;
  warehouseId: string;
  value: string | null;
  onChange: (id: string | null, nodeId: string | null) => void;
}

function AssembledBundleForm({
  catalogItemId,
  warehouseId,
  value,
  onChange,
}: AssembledBundleFormProps) {
  const [searchString, setSearchString] = useState("");
  const debouncedSearch = useDebounce(searchString, 300);
  const [nodeFilter, setNodeFilter] = useState<{id: string; path: string} | null>(null);

  const query = useQuery({
    ...inventoryItemsGetAllAssembledBundlesOptions({
      query: {
        catalogItemId,
        warehouseId,
        pageSize: 50,
        searchString: debouncedSearch || undefined,
        nodeId: nodeFilter?.id,
      },
    }),
  });

  return (
    <Stack spacing={1}>
      <TextField
        label="Поиск"
        size="small"
        value={searchString}
        onChange={(e) => setSearchString(e.target.value)}
        fullWidth
      />
      <NodeField
        label="Фильтр по ячейке"
        warehouseId={warehouseId}
        nodePath={nodeFilter?.path ?? null}
        onSelect={(node) =>
          setNodeFilter({id: node.nodeId, path: formatStoragePlaceNodeName(node.nodePath)})
        }
        onClear={() => setNodeFilter(null)}
      />
      <FormControl size="small" fullWidth>
        <InputLabel>Готовый комплект</InputLabel>
        <Select
          value={value ?? ""}
          onChange={(e) => {
            const item = query.data?.items.find((i) => i.id === e.target.value);
            onChange(e.target.value || null, item?.nodeId ?? null);
          }}
          label="Готовый комплект"
          disabled={query.isLoading}
        >
          {query.data?.items.map((item) => (
            <MenuItem key={item.id} value={item.id}>
              {item.storagePlaceName} / {item.nodeName}
            </MenuItem>
          ))}
          {!query.isLoading && !query.data?.items.length && (
            <MenuItem disabled>Нет доступных комплектов</MenuItem>
          )}
        </Select>
      </FormControl>
    </Stack>
  );
}

// ─── Bundle slot (recursive — handles standard/unit/nested bundle/variation) ─

interface BundleSlotFormProps {
  warehouseId: string;
  catalogItemId: string;
  catalogItemType: CatalogItemType;
  componentName: string;
  multiplier: number;
  onChange: (entries: AddFulfillmentBundleComponentRequest[]) => void;
}

function BundleSlotForm({
  warehouseId,
  catalogItemId,
  catalogItemType,
  componentName,
  multiplier,
  onChange,
}: BundleSlotFormProps) {
  const [nodePath, setNodePath] = useState<string | null>(null);
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

  if (catalogItemType === "standard") {
    return (
      <NodeField
        label={`${componentName} × ${multiplier}`}
        warehouseId={warehouseId}
        nodePath={nodePath}
        onSelect={(node) => {
          setNodePath(formatStoragePlaceNodeName(node.nodePath));
          onChange([
            {
              catalogItemId,
              sourceNodeId: node.nodeId,
              quantity: multiplier,
              unitInventoryItemId: null,
            },
          ]);
        }}
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
          onChange={onChange}
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
        <FormControl size="small" fullWidth>
          <InputLabel>{componentName} — вариант</InputLabel>
          <Select
            value={selectedVariantId ?? ""}
            label={`${componentName} — вариант`}
            onChange={(e) => {
              const variant = variantOptions.find((v) => v.id === e.target.value);
              setSelectedVariantId(e.target.value || null);
              setSelectedVariantType(variant?.type ?? null);
              onChange([]);
            }}
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
          (selectedVariantType === "assembledBundle" ? (
            <Alert severity="warning">
              Вариант "Готовый комплект" внутри состава комплекта не поддерживается
            </Alert>
          ) : (
            <BundleSlotForm
              key={selectedVariantId}
              warehouseId={warehouseId}
              catalogItemId={selectedVariantId}
              catalogItemType={selectedVariantType}
              componentName={componentName}
              multiplier={multiplier}
              onChange={onChange}
            />
          ))}
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
  onChange: (entries: AddFulfillmentBundleComponentRequest[]) => void;
}

function BundleTreeForm({
  catalogItemId,
  warehouseId,
  multiplier = 1,
  onChange,
}: BundleTreeFormProps) {
  const catalogQuery = useQuery(catalogGetByIdOptions({path: {id: catalogItemId}}));
  const components = catalogQuery.data?.components ?? [];
  const slotEntriesRef = useRef<Map<string, AddFulfillmentBundleComponentRequest[]>>(new Map());

  function updateSlot(componentId: string, entries: AddFulfillmentBundleComponentRequest[]) {
    slotEntriesRef.current.set(componentId, entries);
    onChange(Array.from(slotEntriesRef.current.values()).flat());
  }

  if (catalogQuery.isLoading) return <CircularProgress size={20} />;

  return (
    <Stack spacing={2}>
      {components.map((comp) => (
        <BundleSlotForm
          key={comp.componentId}
          warehouseId={warehouseId}
          catalogItemId={comp.componentId}
          catalogItemType={comp.componentType}
          componentName={comp.componentName}
          multiplier={comp.quantity * multiplier}
          onChange={(entries) => updateSlot(comp.componentId, entries)}
        />
      ))}
    </Stack>
  );
}

// ─── Variation fulfillment ────────────────────────────────────────────────

interface VariationFormProps {
  catalogItemId: string;
  warehouseId: string;
  fulfillment: AddFulfillmentRequest;
  onFulfillmentChange: (f: AddFulfillmentRequest) => void;
}

function VariationForm({
  catalogItemId,
  warehouseId,
  fulfillment,
  onFulfillmentChange,
}: VariationFormProps) {
  const [selectedVariantId, setSelectedVariantId] = useState<string | null>(null);
  const [selectedVariantType, setSelectedVariantType] = useState<CatalogItemType | null>(null);

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
      <FormControl size="small" fullWidth>
        <InputLabel>Вариант</InputLabel>
        <Select
          value={selectedVariantId ?? ""}
          onChange={(e) => {
            const variant = variantOptions.find((v) => v.id === e.target.value);
            setSelectedVariantId(e.target.value || null);
            setSelectedVariantType(variant?.type ?? null);
            onFulfillmentChange({sourceNodeId: null, quantity: 0});
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
              fulfillment={fulfillment}
              onChange={onFulfillmentChange}
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
  fulfillment: AddFulfillmentRequest;
  onChange: (f: AddFulfillmentRequest) => void;
}

function SubFulfillmentForm({
  catalogItemId,
  catalogItemType,
  warehouseId,
  fulfillment,
  onChange,
}: SubFulfillmentFormProps) {
  const [bundleMode, setBundleMode] = useState<"tree" | "assembled">("tree");
  const [nodeState, setNodeState] = useState<{
    sourceNodeId: string | null;
    nodePath: string | null;
    quantity: number;
  }>({
    sourceNodeId: fulfillment.sourceNodeId ?? null,
    nodePath: null,
    quantity: fulfillment.quantity,
  });

  if (catalogItemType === "standard") {
    return (
      <StandardForm
        warehouseId={warehouseId}
        quantity={fulfillment.quantity}
        value={nodeState}
        onChange={(v) => {
          setNodeState(v);
          onChange({sourceNodeId: v.sourceNodeId, quantity: v.quantity});
        }}
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
          onChange({sourceNodeId: nodeId, quantity: 0, unitInventoryItemId: id})
        }
      />
    );
  }

  if (catalogItemType === "assembledBundle") {
    return (
      <AssembledBundleForm
        catalogItemId={catalogItemId}
        warehouseId={warehouseId}
        value={fulfillment.assembledBundleInventoryItemId ?? null}
        onChange={(id, nodeId) =>
          onChange({sourceNodeId: nodeId, quantity: 0, assembledBundleInventoryItemId: id})
        }
      />
    );
  }

  if (catalogItemType === "bundle") {
    return (
      <Stack spacing={2}>
        <ToggleButtonGroup
          size="small"
          value={bundleMode}
          exclusive
          onChange={(_, v) => v && setBundleMode(v)}
        >
          <ToggleButton value="tree">Собрать из компонентов</ToggleButton>
          <ToggleButton value="assembled">Готовый комплект</ToggleButton>
        </ToggleButtonGroup>

        {bundleMode === "assembled" ? (
          <AssembledBundleForm
            catalogItemId={catalogItemId}
            warehouseId={warehouseId}
            value={fulfillment.assembledBundleInventoryItemId ?? null}
            onChange={(id, nodeId) =>
              onChange({sourceNodeId: nodeId, quantity: 0, assembledBundleInventoryItemId: id})
            }
          />
        ) : (
          <BundleTreeForm
            key={catalogItemId}
            catalogItemId={catalogItemId}
            warehouseId={warehouseId}
            onChange={(comps) =>
              onChange({sourceNodeId: null, quantity: 0, bundleComponents: comps})
            }
          />
        )}
      </Stack>
    );
  }

  return null;
}

// ─── Main dialog ──────────────────────────────────────────────────────────

function AddFulfillmentDialog({
  open,
  onClose,
  orderId,
  warehouseId,
  taskId,
  taskBoxId,
  component,
}: AddFulfillmentDialogProps) {
  const queryClient = useQueryClient();
  const [fulfillment, setFulfillment] = useState<AddFulfillmentRequest>({
    sourceNodeId: null,
    quantity: component.quantity,
  });
  const [bundleCount, setBundleCount] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [failedItems, setFailedItems] = useState<BatchFulfillFailedItem[]>([]);

  const mutation = useMutation({
    ...ordersAddFulfillmentMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: ordersGetAllAssemblyQueryKey()});
      queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id: orderId}})});
      onClose();
    },
    onError: () => setError("Не удалось добавить фулфилмент"),
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
    onError: () => setError("Не удалось добавить фулфилмент"),
  });

  const isVariation = component.catalogItemType === "variation";
  const isBundleFulfillment = (fulfillment.bundleComponents?.length ?? 0) > 0;
  const remaining = component.quantity - countFulfilledQty(component.fulfillments);

  function handleSubmit() {
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
      batchMutation.mutate({body: {items}});
    } else {
      mutation.mutate({
        path: {id: orderId, taskId, tbid: taskBoxId, cid: component.id},
        body: fulfillment,
      });
    }
  }

  const canSubmit =
    !!fulfillment.sourceNodeId && fulfillment.quantity > 0
      ? true
      : !!fulfillment.unitInventoryItemId ||
        !!fulfillment.assembledBundleInventoryItemId ||
        isBundleFulfillment;

  const isPending = mutation.isPending || batchMutation.isPending;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Добавить фулфилмент — {component.catalogItemName}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          {isVariation ? (
            <VariationForm
              catalogItemId={component.catalogItemId}
              warehouseId={warehouseId}
              fulfillment={fulfillment}
              onFulfillmentChange={setFulfillment}
            />
          ) : (
            <SubFulfillmentForm
              catalogItemId={component.catalogItemId}
              catalogItemType={component.catalogItemType}
              warehouseId={warehouseId}
              fulfillment={fulfillment}
              onChange={setFulfillment}
            />
          )}

          {isBundleFulfillment && (
            <TextField
              label="Количество комплектов для сборки"
              type="number"
              size="small"
              value={bundleCount}
              onChange={(e) =>
                setBundleCount(Math.max(1, Math.min(remaining, Number(e.target.value) || 1)))
              }
              helperText={
                bundleCount > 1
                  ? `Будет создано ${bundleCount} одинаковых фулфилментов комплекта`
                  : undefined
              }
              slotProps={{htmlInput: {min: 1, max: Math.max(1, remaining)}}}
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
                  • {f.error}
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
    </Dialog>
  );
}

export {UnitForm, AssembledBundleForm, BundleTreeForm, VariationForm};
export default AddFulfillmentDialog;
