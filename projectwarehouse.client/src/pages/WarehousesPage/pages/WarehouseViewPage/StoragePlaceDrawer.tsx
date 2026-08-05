import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  IconButton,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {Link} from "react-router";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  storagePlacesAddNodeMutation,
  storagePlacesDeleteNodeMutation,
  storagePlacesGetNodesOptions,
  storagePlacesGetNodesQueryKey,
  storagePlacesReorderNodesMutation,
  storagePlacesUpdateNodeMutation,
  warehousesGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen.ts";
import {type NodeOrderItem, type StoragePlaceDto, type StoragePlaceNodeDto} from "@/api";
import {useState, useRef, useEffect} from "react";
import {StoragePlaceNodeTree} from "@/features/warehouse";
import {openPrintPage} from "@/utils/printUtils.ts";
import {formatEntityBarcode} from "@/utils/barcodeUtils.ts";
import AddIcon from "@mui/icons-material/Add";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import EditIcon from "@mui/icons-material/Edit";
import PrintIcon from "@mui/icons-material/Print";
import Inventory2Icon from "@mui/icons-material/Inventory2";
import ConfirmDialog from "@/components/ConfirmDialog.tsx";
import {SortableNodeTree} from "./SortableNodeTree.tsx";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";

const DRAWER_WIDTH = 1000;

interface StoragePlaceDialogProps {
  open: boolean;
  storagePlace?: StoragePlaceDto;
  warehouseId: string;
  onClose: () => void;
}

type NodeDialogState =
  | {mode: "addRoot"}
  | {mode: "addChild"; parentId: string}
  | {mode: "rename"; node: StoragePlaceNodeDto};

function StoragePlaceDrawer({open, storagePlace, warehouseId, onClose}: StoragePlaceDialogProps) {
  const queryClient = useQueryClient();

  const {data: nodes = [], isLoading} = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: storagePlace?.id ?? ""}}),
    enabled: open && !!storagePlace?.id,
  });

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const nodeAutoSelected = useRef(false);
  const [prevStoragePlaceId, setPrevStoragePlaceId] = useState(storagePlace?.id);
  if (prevStoragePlaceId !== storagePlace?.id) {
    setPrevStoragePlaceId(storagePlace?.id);
    setSelectedNodeId(null);
  }

  const [isTreeEditMode, setIsTreeEditMode] = useState(false);
  const [nodeDialog, setNodeDialog] = useState<NodeDialogState | null>(null);
  const [nodeName, setNodeName] = useState("");
  const [deleteTarget, setDeleteTarget] = useState<StoragePlaceNodeDto | null>(null);

  const handleClose = () => {
    onClose();
    setSelectedNodeId(null);
    setIsTreeEditMode(false);
    nodeAutoSelected.current = false;
  };

  useEffect(() => {
    if (nodes && !nodeAutoSelected.current && !selectedNodeId) {
      setSelectedNodeId(nodes.filter((x) => !x.parentNodeId)[0]?.id ?? null);
    }
  }, [nodes, selectedNodeId]);

  const updateNodesCache = (updatedNodes: StoragePlaceNodeDto[]) => {
    queryClient.setQueryData(
      storagePlacesGetNodesQueryKey({path: {id: storagePlace!.id}}),
      updatedNodes,
    );
  };

  const addMutation = useMutation({
    ...storagePlacesAddNodeMutation(),
    onSuccess: updateNodesCache,
  });

  const updateMutation = useMutation({
    ...storagePlacesUpdateNodeMutation(),
    onSuccess: updateNodesCache,
  });

  const deleteMutation = useMutation({
    ...storagePlacesDeleteNodeMutation(),
    onSuccess: (updatedNodes) => {
      updateNodesCache(updatedNodes);
      void queryClient.invalidateQueries({
        queryKey: warehousesGetByIdQueryKey({path: {id: warehouseId}}),
      });
    },
  });

  const reorderMutation = useMutation({
    ...storagePlacesReorderNodesMutation(),
    onSuccess: updateNodesCache,
    onError: () => {
      void queryClient.invalidateQueries({
        queryKey: storagePlacesGetNodesQueryKey({path: {id: storagePlace!.id}}),
      });
    },
  });

  const isPending =
    addMutation.isPending ||
    updateMutation.isPending ||
    deleteMutation.isPending ||
    reorderMutation.isPending;

  const openAddRoot = () => {
    setNodeName("");
    setNodeDialog({mode: "addRoot"});
  };

  const openAddChild = (parentId: string) => {
    setNodeName("");
    setNodeDialog({mode: "addChild", parentId});
  };

  const openRename = (node: StoragePlaceNodeDto) => {
    setNodeName(node.name);
    setNodeDialog({mode: "rename", node});
  };

  const closeNodeDialog = () => {
    setNodeDialog(null);
    setNodeName("");
  };

  const handleNodeDialogSubmit = () => {
    if (!storagePlace?.id || !nodeName.trim()) return;

    if (nodeDialog?.mode === "addRoot") {
      const order = nodes.filter((n) => !n.parentNodeId).length;
      addMutation.mutate(
        {path: {id: storagePlace.id}, body: {name: nodeName.trim(), parentNodeId: null, order}},
        {onSuccess: closeNodeDialog},
      );
    } else if (nodeDialog?.mode === "addChild") {
      const order = nodes.filter((n) => n.parentNodeId === nodeDialog.parentId).length;
      addMutation.mutate(
        {
          path: {id: storagePlace.id},
          body: {name: nodeName.trim(), parentNodeId: nodeDialog.parentId, order},
        },
        {onSuccess: closeNodeDialog},
      );
    } else if (nodeDialog?.mode === "rename") {
      updateMutation.mutate(
        {
          path: {id: storagePlace.id, nodeId: nodeDialog.node.id},
          body: {
            name: nodeName.trim(),
            parentNodeId: nodeDialog.node.parentNodeId ?? null,
            order: nodeDialog.node.order,
          },
        },
        {onSuccess: closeNodeDialog},
      );
    }
  };

  const handleDeleteRequest = (node: StoragePlaceNodeDto) => {
    setDeleteTarget(node);
  };

  const handleDeleteConfirm = () => {
    if (!storagePlace?.id || !deleteTarget) return;
    if (selectedNodeId === deleteTarget.id) setSelectedNodeId(null);
    deleteMutation.mutate(
      {path: {id: storagePlace.id, nodeId: deleteTarget.id}},
      {onSuccess: () => setDeleteTarget(null)},
    );
  };

  const handleReorder = (items: NodeOrderItem[]) => {
    if (!storagePlace?.id) return;
    const orderMap = new Map(items.map((x) => [x.nodeId, x.order]));
    updateNodesCache(
      nodes.map((n) => (orderMap.has(n.id) ? {...n, order: orderMap.get(n.id)!} : n)),
    );
    reorderMutation.mutate({path: {id: storagePlace.id}, body: items});
  };

  const printLabels = () => {
    const nodeMap = new Map(nodes.map((n) => [n.id, n]));
    const buildPath = (node: StoragePlaceNodeDto): string => {
      const parts: string[] = [];
      let current: StoragePlaceNodeDto | undefined = node;
      while (current) {
        parts.unshift(current.name);
        current = current.parentNodeId ? nodeMap.get(current.parentNodeId) : undefined;
      }
      return parts.join(" / ");
    };
    openPrintPage(
      nodes.map((x) => ({
        type: "DataMatrix",
        value: formatEntityBarcode("storagePlaceNode", x.id),
        label: `${storagePlace?.name} / ${buildPath(x)}`,
      })),
    );
  };

  const treeActions = {
    onAddChild: openAddChild,
    onRename: openRename,
    onDelete: handleDeleteRequest,
    onReorder: handleReorder,
    isDisabled: isPending,
  };

  const nodeDialogTitle =
    nodeDialog?.mode === "rename"
      ? "Переименовать ячейку"
      : nodeDialog?.mode === "addChild"
        ? "Добавить дочернюю ячейку"
        : "Добавить ячейку";

  return (
    <>
      <Drawer
        anchor="right"
        open={open}
        onClose={handleClose}
        slotProps={{
          paper: {
            sx: {
              width: DRAWER_WIDTH,
              maxWidth: "calc(100vw - 10px)",
              display: "flex",
              flexDirection: "column",
            },
          },
        }}
      >
        <Stack
          direction="row"
          sx={{
            alignItems: "center",
            justifyContent: "space-between",
            px: 2,
            py: 1.5,
            flexShrink: 0,
          }}
        >
          <Typography variant="h6" noWrap sx={{flex: 1, mr: 1}}>
            {storagePlace?.name ?? ""}
          </Typography>
          <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
            {selectedNodeId && (
              <Button
                size="small"
                startIcon={<Inventory2Icon />}
                component={Link}
                to={`/storage/warehouses/${warehouseId}/storage-places/${storagePlace?.id}/nodes/${selectedNodeId}/inventory`}
                disabled={!!nodes.find((x) => x.parentNodeId == selectedNodeId)}
              >
                Остатки ячейки
              </Button>
            )}
            <Button
              size="small"
              startIcon={<Inventory2Icon />}
              component={Link}
              to={`/storage/warehouses/${warehouseId}/storage-places/${storagePlace?.id}/inventory`}
              disabled={!storagePlace?.id}
            >
              Остатки
            </Button>
            <Button
              size="small"
              startIcon={<PrintIcon />}
              disabled={isLoading || nodes.length === 0}
              onClick={printLabels}
            >
              Этикетки
            </Button>
            {isTreeEditMode ? (
              <Button
                size="small"
                startIcon={<CheckIcon />}
                color="success"
                onClick={() => setIsTreeEditMode(false)}
              >
                Готово
              </Button>
            ) : (
              <Button size="small" startIcon={<EditIcon />} onClick={() => setIsTreeEditMode(true)}>
                Редактировать ячейки
              </Button>
            )}
            <IconButton onClick={handleClose} size="small">
              <CloseIcon />
            </IconButton>
          </Stack>
        </Stack>
        <Divider />
        {isTreeEditMode && (
          <Alert sx={{mt: 1}} severity={"info"}>
            Ячейки редактируются сразу
          </Alert>
        )}
        <Box sx={{overflowY: "auto", flex: 1, p: 2}}>
          <Stack spacing={2}>
            <Stack spacing={1}>
              <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                <Typography variant="subtitle2">Ячейки</Typography>
                {nodes.length > 0 && <Chip label={nodes.length} size="small" />}
                {(storagePlace?.totalItemsCount ?? 0) > 0 && (
                  <Chip
                    label={pluralCount(storagePlace!.totalItemsCount, NOUNS.item)}
                    size="small"
                    color="primary"
                    variant="outlined"
                  />
                )}
                {isTreeEditMode && (
                  <>
                    <Box sx={{flex: 1}} />
                    <Button
                      size="small"
                      startIcon={<AddIcon />}
                      disabled={isPending}
                      onClick={openAddRoot}
                    >
                      Добавить ячейку
                    </Button>
                  </>
                )}
              </Stack>
              {isLoading ? (
                <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
                  <CircularProgress size={32} />
                </Box>
              ) : nodes.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  Ячейки не найдены
                </Typography>
              ) : (
                <Paper variant="outlined" sx={{p: 1}}>
                  {isTreeEditMode ? (
                    <SortableNodeTree nodes={nodes} actions={treeActions} />
                  ) : (
                    <StoragePlaceNodeTree
                      nodes={nodes}
                      selectedNodeId={selectedNodeId}
                      onSelect={setSelectedNodeId}
                    />
                  )}
                </Paper>
              )}
            </Stack>
          </Stack>
        </Box>
      </Drawer>

      <Dialog open={nodeDialog !== null} onClose={closeNodeDialog} maxWidth="xs" fullWidth>
        <DialogTitle>{nodeDialogTitle}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            size="small"
            label="Название"
            value={nodeName}
            onChange={(e) => setNodeName(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") handleNodeDialogSubmit();
            }}
            disabled={isPending}
            sx={{mt: 1}}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={closeNodeDialog} disabled={isPending}>
            Отмена
          </Button>
          <Button
            variant="contained"
            onClick={handleNodeDialogSubmit}
            disabled={!nodeName.trim() || isPending}
            loading={isPending}
          >
            {nodeDialog?.mode === "rename" ? "Сохранить" : "Добавить"}
          </Button>
        </DialogActions>
      </Dialog>

      <ConfirmDialog
        open={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        title="Удалить ячейку?"
        onConfirm={handleDeleteConfirm}
        isPending={deleteMutation.isPending}
        confirmText="Удалить"
        confirmColor="error"
      >
        Ячейка «{deleteTarget?.name}» будет удалена безвозвратно.
      </ConfirmDialog>
    </>
  );
}

export default StoragePlaceDrawer;
