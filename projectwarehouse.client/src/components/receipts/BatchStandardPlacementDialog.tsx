import React, {useState, useMemo} from "react";
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
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  receiptsAddStandardPlacementBatchMutation,
  warehousesGetDefaultNodeOptions,
} from "@/api/@tanstack/react-query.gen";
import type {ReceiptDto, ReceiptItemDto} from "@/api/types.gen";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import type {SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import {extractErrorMessage} from "@/utils/errorUtils";

function calcTotalPlaced(item: ReceiptItemDto): number {
  return item.placements.reduce(
    (sum, p) =>
      sum + (p.count || (p.unitInventoryItemId || p.assembledBundleInventoryItemId ? 1 : 0)),
    0,
  );
}

function calcBatchCount(item: ReceiptItemDto): number {
  return (item.receivedCount ?? item.plannedCount) - calcTotalPlaced(item);
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

interface BatchStandardPlacementDialogProps {
  open: boolean;
  onClose: () => void;
  receiptId: string;
  warehouseId: string;
  items: ReceiptItemDto[];
  onUpdate: (receipt: ReceiptDto) => void;
}

function BatchStandardPlacementDialog({
  open,
  onClose,
  receiptId,
  warehouseId,
  items,
  onUpdate,
}: BatchStandardPlacementDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const [selectedNode, setSelectedNode] = useState<SelectedNode | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const initialCounts = useMemo(
    () =>
      Object.fromEntries(items.map((item) => [item.id, String(Math.max(1, calcBatchCount(item)))])),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );
  const [counts, setCounts] = useState<Record<string, string>>(initialCounts);

  const defaultNodeQuery = useQuery({
    ...warehousesGetDefaultNodeOptions({path: {id: warehouseId}}),
    enabled: open,
    meta: {suppressGlobalError: true},
    retry: false,
  });

  const defaultNode: SelectedNode | null = useMemo(
    () =>
      defaultNodeQuery.data
        ? {nodeId: defaultNodeQuery.data.id, nodePath: defaultNodeQuery.data.name}
        : null,
    [defaultNodeQuery.data],
  );

  const effectiveNode = selectedNode ?? defaultNode;

  const mutation = useMutation({
    ...receiptsAddStandardPlacementBatchMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onError: (err) => setErrorMsg(extractErrorMessage(err)),
  });

  const handleSubmit = () => {
    if (!effectiveNode) {
      setErrorMsg("Выберите ячейку");
      return;
    }
    const itemsToPlace = items
      .map((item) => ({itemId: item.id, count: parseInt(counts[item.id] ?? "0", 10)}))
      .filter(({count}) => !isNaN(count) && count > 0);
    if (itemsToPlace.length === 0) {
      setErrorMsg("Укажите количество хотя бы для одной позиции");
      return;
    }
    setErrorMsg(null);
    mutation.mutate({
      path: {id: receiptId},
      body: {storagePlaceNodeId: effectiveNode.nodeId, items: itemsToPlace},
    });
  };

  const isReady = !defaultNodeQuery.isPending;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth fullScreen={isMobile}>
      <DialogTitle>Разместить позиции ({items.length})</DialogTitle>
      <DialogContent>
        {!isReady ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
            <CircularProgress size={32} />
          </Box>
        ) : (
          <Stack spacing={2} sx={{pt: 1}}>
            <Stack spacing={0.5}>
              <Typography variant="body2" color="text.secondary">
                Ячейка
              </Typography>
              <NodeSelector
                node={effectiveNode}
                onSelect={setSelectedNode}
                warehouseId={warehouseId}
              />
            </Stack>

            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Товар</TableCell>
                  <TableCell align="right" sx={{width: 96}}>
                    Количество
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>{item.catalogItem.fullName}</TableCell>
                    <TableCell align="right">
                      <TextField
                        value={counts[item.id] ?? ""}
                        onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                          setCounts((prev) => ({...prev, [item.id]: e.target.value}))
                        }
                        type="number"
                        size="small"
                        sx={{width: 80}}
                        disabled={mutation.isPending}
                        slotProps={{htmlInput: {min: 1}}}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>

            {errorMsg && <Alert severity="error">{errorMsg}</Alert>}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={mutation.isPending || !isReady}
        >
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Разместить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default BatchStandardPlacementDialog;
