import {useState} from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import DeleteIcon from "@mui/icons-material/Delete";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  writeoffsSyncItemsMutation,
  writeoffsGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen";
import type {WriteoffDto, WriteoffItemDto, WriteoffItemRequest} from "@/api/types.gen";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import InventoryItemPickerModal from "@/components/inventory/InventoryItemPickerModal";
import type {SelectedInventoryItem} from "@/components/inventory/InventoryItemPickerModal";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import type {SelectedNode} from "@/components/receipts/SelectNodeModal";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface WriteoffItemsEditorDrawerProps {
  open: boolean;
  onClose: () => void;
  writeoff: WriteoffDto;
}

type DraftItem =
  | {
      type: "standard";
      key: string;
      sourceNodeId: string;
      sourceNodePath: string[];
      catalogItemId: string;
      catalogItemName: string;
      count: number;
      available: number;
      notes: string;
    }
  | {
      type: "unit";
      key: string;
      sourceNodeId: string;
      sourceNodePath: string[];
      unitItemId: string;
      inventoryNumber: string;
      catalogItemName: string;
      notes: string;
    };

function existingItemsToDraft(items: WriteoffItemDto[]): DraftItem[] {
  return items.map((item): DraftItem => {
    if (item.unitInventoryItemId) {
      return {
        type: "unit",
        key: item.id,
        sourceNodeId: item.sourceNodeId,
        sourceNodePath: item.sourceNodePath,
        unitItemId: item.unitInventoryItemId,
        inventoryNumber: item.inventoryNumber ?? "",
        catalogItemName: item.catalogItemName,
        notes: item.notes ?? "",
      };
    }
    return {
      type: "standard",
      key: item.id,
      sourceNodeId: item.sourceNodeId,
      sourceNodePath: item.sourceNodePath,
      catalogItemId: item.catalogItemId!,
      catalogItemName: item.catalogItemName,
      count: item.count,
      available: item.count,
      notes: item.notes ?? "",
    };
  });
}

function draftToRequest(items: DraftItem[]): WriteoffItemRequest[] {
  return items.map((item) => {
    const base: WriteoffItemRequest = {
      sourceNodeId: item.sourceNodeId,
      notes: item.notes || null,
    };
    if (item.type === "standard") {
      return {...base, catalogItemId: item.catalogItemId, count: item.count};
    }
    return {...base, unitInventoryItemId: item.unitItemId};
  });
}

function itemLabel(item: DraftItem): string {
  if (item.type === "unit") return `${item.catalogItemName} [${item.inventoryNumber}]`;
  return item.catalogItemName;
}

function WriteoffItemsEditorDrawer({open, onClose, writeoff}: WriteoffItemsEditorDrawerProps) {
  const {enqueueSnackbar} = useSnackbar();
  const queryClient = useQueryClient();

  const [draftItems, setDraftItems] = useState<DraftItem[]>(() =>
    existingItemsToDraft(writeoff.items),
  );

  const [nodeModalOpen, setNodeModalOpen] = useState(false);
  const [pickerNode, setPickerNode] = useState<SelectedNode | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);

  const mutation = useMutation({
    ...writeoffsSyncItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      queryClient.setQueryData(writeoffsGetByIdQueryKey({path: {id: writeoff.id}}), data);
      enqueueSnackbar("Список товаров обновлён", {variant: "success"});
      onClose();
    },
    onError: () => {
      enqueueSnackbar("Не удалось сохранить список товаров", {variant: "error"});
    },
  });

  const handleNodeSelect = (node: SelectedNode) => {
    setPickerNode(node);
    setPickerOpen(true);
  };

  const handlePickerConfirm = (picked: SelectedInventoryItem[]) => {
    if (!pickerNode) return;
    setDraftItems((prev) => {
      const next = [...prev];
      for (const item of picked) {
        if (item.type === "standard") {
          const existing = next.find(
            (d) =>
              d.type === "standard" &&
              d.catalogItemId === item.catalogItemId &&
              d.sourceNodeId === pickerNode.nodeId,
          ) as (DraftItem & {type: "standard"}) | undefined;
          if (existing) {
            existing.available = item.available;
            existing.count = Math.min(existing.count + item.count, item.available);
          } else {
            next.push({
              type: "standard",
              key: `${pickerNode.nodeId}-${item.catalogItemId}-${Date.now()}`,
              sourceNodeId: pickerNode.nodeId,
              sourceNodePath: pickerNode.nodePath,
              catalogItemId: item.catalogItemId,
              catalogItemName: item.catalogItemName,
              count: item.count,
              available: item.available,
              notes: "",
            });
          }
        } else {
          if (!next.some((d) => d.type === "unit" && d.unitItemId === item.unitItemId)) {
            next.push({
              type: "unit",
              key: item.unitItemId,
              sourceNodeId: pickerNode.nodeId,
              sourceNodePath: pickerNode.nodePath,
              unitItemId: item.unitItemId,
              inventoryNumber: item.inventoryNumber,
              catalogItemName: item.catalogItemName,
              notes: "",
            });
          }
        }
      }
      return next;
    });
    setPickerOpen(false);
    setPickerNode(null);
    setNodeModalOpen(false);
  };

  const handleRemove = (key: string) => {
    setDraftItems((prev) => prev.filter((d) => d.key !== key));
  };

  const handleCountChange = (key: string, value: string) => {
    const parsed = parseInt(value, 10);
    setDraftItems((prev) =>
      prev.map((d) => {
        if (d.key !== key || d.type !== "standard") return d;
        return {...d, count: isNaN(parsed) ? 1 : Math.max(1, Math.min(parsed, d.available))};
      }),
    );
  };

  const handleNotesChange = (key: string, value: string) => {
    setDraftItems((prev) => prev.map((d) => (d.key === key ? {...d, notes: value} : d)));
  };

  const handleSave = () => {
    mutation.mutate({
      path: {id: writeoff.id},
      body: draftToRequest(draftItems),
    });
  };

  const handleClose = () => {
    setDraftItems(existingItemsToDraft(writeoff.items));
    onClose();
  };

  // Group items by source node for display
  const groupedByNode = draftItems.reduce<Map<string, {path: string[]; items: DraftItem[]}>>(
    (acc, item) => {
      if (!acc.has(item.sourceNodeId)) {
        acc.set(item.sourceNodeId, {path: item.sourceNodePath, items: []});
      }
      acc.get(item.sourceNodeId)!.items.push(item);
      return acc;
    },
    new Map(),
  );

  return (
    <>
      <Drawer anchor="right" open={open} onClose={handleClose}>
        <Box sx={{width: 580, display: "flex", flexDirection: "column", height: "100%"}}>
          <Stack
            direction="row"
            sx={{alignItems: "center", px: 2, py: 1.5, borderBottom: 1, borderColor: "divider"}}
          >
            <Typography variant="h6" sx={{flexGrow: 1}}>
              Товары к списанию
            </Typography>
            <IconButton onClick={handleClose} size="small">
              <CloseIcon />
            </IconButton>
          </Stack>

          <Box sx={{flex: 1, overflow: "auto", p: 2}}>
            <Stack spacing={2}>
              <Button
                variant="outlined"
                startIcon={<AddIcon />}
                onClick={() => setNodeModalOpen(true)}
                disabled={mutation.isPending}
              >
                Добавить товары из ячейки
              </Button>

              {draftItems.length === 0 ? (
                <Alert severity="info">Нет товаров. Выберите ячейку и добавьте позиции.</Alert>
              ) : (
                [...groupedByNode.entries()].map(([nodeId, {path, items}]) => (
                  <Stack key={nodeId} spacing={1}>
                    <Typography variant="caption" color="text.secondary">
                      {formatStoragePlaceNodeName(path)}
                    </Typography>
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Товар</TableCell>
                          <TableCell sx={{width: 80}}>Кол-во</TableCell>
                          <TableCell sx={{width: 180}}>Примечания</TableCell>
                          <TableCell sx={{width: 40}} />
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {items.map((item) => (
                          <TableRow key={item.key}>
                            <TableCell>
                              <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                                <CatalogItemTypeChip type={item.type} />
                                <Typography variant="body2">{itemLabel(item)}</Typography>
                              </Stack>
                            </TableCell>
                            <TableCell>
                              {item.type === "standard" ? (
                                <TextField
                                  size="small"
                                  type="number"
                                  value={item.count}
                                  onChange={(e) => handleCountChange(item.key, e.target.value)}
                                  slotProps={{htmlInput: {min: 1, max: item.available}}}
                                  sx={{width: 72}}
                                  disabled={mutation.isPending}
                                />
                              ) : (
                                "1"
                              )}
                            </TableCell>
                            <TableCell>
                              <TextField
                                size="small"
                                value={item.notes}
                                onChange={(e) => handleNotesChange(item.key, e.target.value)}
                                placeholder="Примечание"
                                sx={{width: "100%"}}
                                disabled={mutation.isPending}
                              />
                            </TableCell>
                            <TableCell>
                              <Tooltip title="Удалить">
                                <IconButton
                                  size="small"
                                  onClick={() => handleRemove(item.key)}
                                  disabled={mutation.isPending}
                                >
                                  <DeleteIcon fontSize="small" />
                                </IconButton>
                              </Tooltip>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                    <Divider />
                  </Stack>
                ))
              )}
            </Stack>
          </Box>

          <Stack
            direction="row"
            spacing={1}
            sx={{p: 2, borderTop: 1, borderColor: "divider", justifyContent: "flex-end"}}
          >
            <Button onClick={handleClose} disabled={mutation.isPending}>
              Отмена
            </Button>
            <Button variant="contained" onClick={handleSave} disabled={mutation.isPending}>
              {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Сохранить"}
            </Button>
          </Stack>
        </Box>
      </Drawer>

      <SelectNodeModal
        open={nodeModalOpen}
        onClose={() => setNodeModalOpen(false)}
        warehouseId={writeoff.warehouseId}
        onSelect={handleNodeSelect}
      />

      {pickerNode && (
        <InventoryItemPickerModal
          open={pickerOpen}
          onClose={() => {
            setPickerOpen(false);
            setPickerNode(null);
          }}
          nodeId={pickerNode.nodeId}
          onConfirm={handlePickerConfirm}
        />
      )}
    </>
  );
}

export default WriteoffItemsEditorDrawer;
