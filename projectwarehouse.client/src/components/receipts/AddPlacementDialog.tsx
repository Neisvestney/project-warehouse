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
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import {useForm} from "react-hook-form";
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  receiptsAddStandardPlacementMutation,
  receiptsAddUnitPlacementMutation,
  warehousesGetDefaultNodeOptions,
} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import type {ReceiptItemDto} from "@/api/types.gen";

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
