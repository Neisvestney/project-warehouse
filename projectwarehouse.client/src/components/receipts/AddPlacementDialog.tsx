import {useState} from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {useForm} from "react-hook-form";
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  catalogGetByIdOptions,
  receiptsAddAssembledBundlePlacementMutation,
  receiptsAddStandardPlacementMutation,
  receiptsAddUnitPlacementMutation,
  warehousesGetDefaultNodeOptions,
} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import type {
  AssembledBundlePlacementComponentRequest,
  BundleComponentDto,
  CatalogItemDto,
  ReceiptItemDto,
} from "@/api/types.gen";

import type {SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface AddPlacementDialogProps {
  open: boolean;
  onClose: () => void;
  receiptId: string;
  item: ReceiptItemDto;
  warehouseId: string;
  onUpdate: (updatedItem: ReceiptItemDto) => void;
}

function NodeSelector({
  node,
  onSelect,
  warehouseId,
}: {
  node: SelectedNode | null;
  onSelect: (node: SelectedNode) => void;
  warehouseId: string;
}) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
        <Typography
          variant="body2"
          sx={{
            flexGrow: 1,
            color: node ? "text.primary" : "text.disabled",
            fontStyle: node ? "normal" : "italic",
          }}
        >
          {node ? formatStoragePlaceNodeName(node.nodePath) : "Ячейка не выбрана"}
        </Typography>
        <Button variant="outlined" size="small" onClick={() => setOpen(true)}>
          Выбрать ячейку
        </Button>
      </Stack>
      <SelectNodeModal
        open={open}
        onClose={() => setOpen(false)}
        warehouseId={warehouseId}
        onSelect={(n) => {
          onSelect(n);
          setOpen(false);
        }}
      />
    </>
  );
}

function StandardPlacementForm({
  item,
  receiptId,
  warehouseId,
  defaultNode,
  onUpdate,
  onClose,
}: {
  item: ReceiptItemDto;
  receiptId: string;
  warehouseId: string;
  defaultNode: SelectedNode | null;
  onUpdate: (item: ReceiptItemDto) => void;
  onClose: () => void;
}) {
  const [selectedNode, setSelectedNode] = useState<SelectedNode | null>(defaultNode);
  const form = useForm<{count: number}>({defaultValues: {count: 1}});
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...receiptsAddStandardPlacementMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!selectedNode) {
      form.setError("root", {message: "Выберите ячейку"});
      return;
    }
    mutation.mutate({
      path: {id: receiptId, itemId: item.id},
      body: {storagePlaceNodeId: selectedNode.nodeId, count: values.count},
    });
  });

  return (
    <Box component="form" onSubmit={onSubmit}>
      <Stack spacing={2}>
        <Stack spacing={0.5}>
          <Typography variant="body2" color="text.secondary">
            Ячейка
          </Typography>
          <NodeSelector node={selectedNode} onSelect={setSelectedNode} warehouseId={warehouseId} />
        </Stack>
        <FormTextField
          control={form.control}
          name="count"
          label="Количество"
          type="number"
          rules={{required: "Обязательное поле", min: {value: 1, message: "Минимум 1"}}}
          disabled={mutation.isPending}
          fullWidth
          slotProps={{htmlInput: {min: 1}}}
        />
        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
      </Stack>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button type="submit" variant="contained" disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Разместить"}
        </Button>
      </DialogActions>
    </Box>
  );
}

function UnitPlacementForm({
  item,
  receiptId,
  warehouseId,
  defaultNode,
  onUpdate,
  onClose,
}: {
  item: ReceiptItemDto;
  receiptId: string;
  warehouseId: string;
  defaultNode: SelectedNode | null;
  onUpdate: (item: ReceiptItemDto) => void;
  onClose: () => void;
}) {
  const [selectedNode, setSelectedNode] = useState<SelectedNode | null>(defaultNode);
  const form = useForm<{inventoryNumber: string}>({defaultValues: {inventoryNumber: ""}});
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...receiptsAddUnitPlacementMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!selectedNode) {
      form.setError("root", {message: "Выберите ячейку"});
      return;
    }
    mutation.mutate({
      path: {id: receiptId, itemId: item.id},
      body: {
        storagePlaceNodeId: selectedNode.nodeId,
        unitItem: {inventoryNumber: values.inventoryNumber},
      },
    });
  });

  return (
    <Box component="form" onSubmit={onSubmit}>
      <Stack spacing={2}>
        <Stack spacing={0.5}>
          <Typography variant="body2" color="text.secondary">
            Ячейка
          </Typography>
          <NodeSelector node={selectedNode} onSelect={setSelectedNode} warehouseId={warehouseId} />
        </Stack>
        <FormTextField
          control={form.control}
          name="inventoryNumber"
          label="Инвентарный номер"
          rules={{required: "Обязательное поле"}}
          disabled={mutation.isPending}
          fullWidth
        />
        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
      </Stack>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button type="submit" variant="contained" disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Разместить"}
        </Button>
      </DialogActions>
    </Box>
  );
}

// Flat list of rows to render in the components table.
// Unit components with quantity N produce N rows (one input each).
// Standard components produce one row (no input, quantity is fixed).
interface ComponentRow {
  key: string;
  componentName: string;
  isUnit: boolean;
  // For unit rows: which inventoryNumbers[] index to bind
  unitFieldIndex: number | null;
  // For unit rows with qty > 1: display label e.g. "1 / 3"
  unitSlotLabel: string | null;
  // For standard rows: fixed quantity
  standardQuantity: number | null;
}

function buildComponentRows(components: BundleComponentDto[]): {
  rows: ComponentRow[];
  totalUnitFields: number;
} {
  const rows: ComponentRow[] = [];
  let unitFieldIndex = 0;

  for (const c of components) {
    if (c.componentType === "unit") {
      for (let slot = 0; slot < c.quantity; slot++) {
        rows.push({
          key: `${c.componentId}-unit-${slot}`,
          componentName: c.componentName,
          isUnit: true,
          unitFieldIndex: unitFieldIndex++,
          unitSlotLabel: c.quantity > 1 ? `${slot + 1} / ${c.quantity}` : null,
          standardQuantity: null,
        });
      }
    } else {
      rows.push({
        key: `${c.componentId}-std`,
        componentName: c.componentName,
        isUnit: false,
        unitFieldIndex: null,
        unitSlotLabel: null,
        standardQuantity: c.quantity,
      });
    }
  }

  return {rows, totalUnitFields: unitFieldIndex};
}

// Inner form — only rendered after catalog item is loaded so defaultValues are stable
function AssembledBundlePlacementFormLoaded({
  catalogItem,
  item,
  receiptId,
  warehouseId,
  defaultNode,
  onUpdate,
  onClose,
}: {
  catalogItem: CatalogItemDto;
  item: ReceiptItemDto;
  receiptId: string;
  warehouseId: string;
  defaultNode: SelectedNode | null;
  onUpdate: (item: ReceiptItemDto) => void;
  onClose: () => void;
}) {
  const [selectedNode, setSelectedNode] = useState<SelectedNode | null>(defaultNode);

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const {rows, totalUnitFields} = buildComponentRows(catalogItem.components);
  const hasOnlyStandard = totalUnitFields === 0;

  const form = useForm<{inventoryNumbers: string[]}>({
    defaultValues: {inventoryNumbers: Array.from({length: totalUnitFields}, () => "")},
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...receiptsAddAssembledBundlePlacementMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!selectedNode) {
      form.setError("root", {message: "Выберите ячейку"});
      return;
    }

    // Build request components: unit → one entry per slot; standard → one entry with quantity
    let unitIdx = 0;
    const components: AssembledBundlePlacementComponentRequest[] = [];
    for (const c of catalogItem.components as BundleComponentDto[]) {
      if (c.componentType === "unit") {
        for (let slot = 0; slot < c.quantity; slot++) {
          components.push({
            catalogItemId: c.componentId,
            quantity: null,
            newUnitItem: {inventoryNumber: values.inventoryNumbers[unitIdx++]},
            unitInventoryItemId: null,
          });
        }
      } else {
        components.push({
          catalogItemId: c.componentId,
          quantity: c.quantity,
          newUnitItem: null,
          unitInventoryItemId: null,
        });
      }
    }

    mutation.mutate({
      path: {id: receiptId, itemId: item.id},
      body: {storagePlaceNodeId: selectedNode.nodeId, components},
    });
  });

  return (
    <Box component="form" onSubmit={onSubmit}>
      <Stack spacing={2}>
        <Stack spacing={0.5}>
          <Typography variant="body2" color="text.secondary">
            Ячейка
          </Typography>
          <NodeSelector node={selectedNode} onSelect={setSelectedNode} warehouseId={warehouseId} />
        </Stack>

        <Divider />

        <Typography variant="body2" color="text.secondary">
          Компоненты
        </Typography>

        {isMobile ? (
          <Stack spacing={1}>
            {rows.map((row) => (
              <Paper key={row.key} variant="outlined" sx={{p: 1.5}}>
                <Stack spacing={1}>
                  <Typography variant="body2">
                    {row.componentName}
                    {row.unitSlotLabel && (
                      <Typography
                        component="span"
                        variant="caption"
                        color="text.secondary"
                        sx={{ml: 0.5}}
                      >
                        ({row.unitSlotLabel})
                      </Typography>
                    )}
                  </Typography>
                  {!hasOnlyStandard && (
                    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                      <Typography variant="body2" color="text.secondary" sx={{minWidth: 60}}>
                        {row.isUnit ? "Инв. номер:" : `Кол-во: ${row.standardQuantity}`}
                      </Typography>
                      {row.isUnit && (
                        <TextField
                          {...form.register(`inventoryNumbers.${row.unitFieldIndex!}`, {
                            required: true,
                          })}
                          size="small"
                          placeholder="Инв. номер"
                          disabled={mutation.isPending}
                          error={!!form.formState.errors.inventoryNumbers?.[row.unitFieldIndex!]}
                          fullWidth
                        />
                      )}
                    </Stack>
                  )}
                  {hasOnlyStandard && (
                    <Typography variant="body2" color="text.secondary">
                      Кол-во: {row.standardQuantity}
                    </Typography>
                  )}
                </Stack>
              </Paper>
            ))}
          </Stack>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Товар</TableCell>
                <TableCell align="right">Кол-во</TableCell>
                {!hasOnlyStandard && <TableCell>Инв. номер</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.key}>
                  <TableCell>
                    {row.componentName}
                    {row.unitSlotLabel && (
                      <Typography
                        component="span"
                        variant="caption"
                        color="text.secondary"
                        sx={{ml: 0.5}}
                      >
                        ({row.unitSlotLabel})
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell align="right">{row.standardQuantity ?? "—"}</TableCell>
                  {!hasOnlyStandard && (
                    <TableCell>
                      {row.isUnit ? (
                        <TextField
                          {...form.register(`inventoryNumbers.${row.unitFieldIndex!}`, {
                            required: true,
                          })}
                          size="small"
                          placeholder="Инв. номер"
                          disabled={mutation.isPending}
                          error={!!form.formState.errors.inventoryNumbers?.[row.unitFieldIndex!]}
                          sx={{width: 140}}
                        />
                      ) : (
                        <Typography variant="body2" color="text.secondary">
                          —
                        </Typography>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {hasOnlyStandard && (
          <Typography variant="body2" color="text.secondary">
            Все компоненты стандартные — инвентарный номер не требуется.
          </Typography>
        )}

        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
      </Stack>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button type="submit" variant="contained" disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Разместить"}
        </Button>
      </DialogActions>
    </Box>
  );
}

function AssembledBundlePlacementForm({
  item,
  receiptId,
  warehouseId,
  defaultNode,
  onUpdate,
  onClose,
}: {
  item: ReceiptItemDto;
  receiptId: string;
  warehouseId: string;
  defaultNode: SelectedNode | null;
  onUpdate: (item: ReceiptItemDto) => void;
  onClose: () => void;
}) {
  const catalogQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: item.catalogItemId}}),
    meta: {suppressGlobalError: true},
  });

  if (catalogQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  if (!catalogQuery.data) return null;

  return (
    <AssembledBundlePlacementFormLoaded
      catalogItem={catalogQuery.data}
      item={item}
      receiptId={receiptId}
      warehouseId={warehouseId}
      defaultNode={defaultNode}
      onUpdate={onUpdate}
      onClose={onClose}
    />
  );
}

const VIRTUAL_TYPES = new Set(["productGroup", "variation", "bundle"]);

function AddPlacementDialog({
  open,
  onClose,
  receiptId,
  item,
  warehouseId,
  onUpdate,
}: AddPlacementDialogProps) {
  const type = item.catalogItem.type;
  const isUnit = type === "unit";
  const isAssembledBundle = type === "assembledBundle";
  const isVirtual = VIRTUAL_TYPES.has(type);

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const defaultNodeQuery = useQuery({
    ...warehousesGetDefaultNodeOptions({path: {id: warehouseId}}),
    enabled: open && !isVirtual,
    meta: {suppressGlobalError: true},
    retry: false,
  });

  const defaultNode: SelectedNode | null = defaultNodeQuery.data
    ? {nodeId: defaultNodeQuery.data.id, nodePath: defaultNodeQuery.data.name}
    : null;

  const isReady = !defaultNodeQuery.isPending || defaultNodeQuery.isError;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth fullScreen={isMobile}>
      <DialogTitle>Добавить размещение — {item.catalogItem.fullName}</DialogTitle>
      <DialogContent>
        {isVirtual ? (
          <Box sx={{py: 2}}>
            <Typography color="text.secondary">
              Товар типа «{type}» является виртуальным и не может быть физически размещён на складе.
            </Typography>
            <DialogActions>
              <Button onClick={onClose}>Закрыть</Button>
            </DialogActions>
          </Box>
        ) : !isReady ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
            <CircularProgress size={32} />
          </Box>
        ) : isUnit ? (
          <UnitPlacementForm
            item={item}
            receiptId={receiptId}
            warehouseId={warehouseId}
            defaultNode={defaultNode}
            onUpdate={onUpdate}
            onClose={onClose}
          />
        ) : isAssembledBundle ? (
          <AssembledBundlePlacementForm
            item={item}
            receiptId={receiptId}
            warehouseId={warehouseId}
            defaultNode={defaultNode}
            onUpdate={onUpdate}
            onClose={onClose}
          />
        ) : (
          <StandardPlacementForm
            item={item}
            receiptId={receiptId}
            warehouseId={warehouseId}
            defaultNode={defaultNode}
            onUpdate={onUpdate}
            onClose={onClose}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

export default AddPlacementDialog;
