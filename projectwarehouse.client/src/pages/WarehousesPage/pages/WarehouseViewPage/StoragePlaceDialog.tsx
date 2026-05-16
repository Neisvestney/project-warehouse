import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
  IconButton,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import {SimpleTreeView} from "@mui/x-tree-view/SimpleTreeView";
import {TreeItem} from "@mui/x-tree-view/TreeItem";
import {useQuery} from "@tanstack/react-query";
import {storagePlacesGetNodesOptions} from "@/api/@tanstack/react-query.gen.ts";
import {type StoragePlaceDto, type StoragePlaceNodeDto} from "@/api";
import {useState} from "react";
import {openPrintPage} from "@/utils/printUtils.ts";
import CloseIcon from "@mui/icons-material/Close";
import PrintIcon from "@mui/icons-material/Print";
import NodeDetails from "./NodeDetails.tsx";

const DRAWER_WIDTH = 1000;

interface StoragePlaceDialogProps {
  open: boolean;
  storagePlace?: StoragePlaceDto;
  warehouseId: string;
  onClose: () => void;
}

function buildRoots(nodes: StoragePlaceNodeDto[]): StoragePlaceNodeDto[] {
  return nodes.filter((n) => !n.parentNodeId);
}

function getChildren(nodes: StoragePlaceNodeDto[], parentId: string): StoragePlaceNodeDto[] {
  return nodes.filter((n) => n.parentNodeId === parentId);
}

function renderNodes(nodes: StoragePlaceNodeDto[], all: StoragePlaceNodeDto[]): React.ReactNode {
  return nodes.map((node) => (
    <TreeItem
      key={node.id}
      itemId={node.id}
      label={
        <Stack direction="row" spacing={1} sx={{alignItems: "center", py: 0.25}}>
          <span>{node.name}</span>
          {node.totalItemsCount > 0 && (
            <Chip label={node.totalItemsCount} size="small" color="primary" variant="outlined" />
          )}
        </Stack>
      }
    >
      {renderNodes(getChildren(all, node.id), all)}
    </TreeItem>
  ));
}

function StoragePlaceDialog({open, storagePlace, warehouseId, onClose}: StoragePlaceDialogProps) {
  const {data: nodes = [], isLoading} = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: storagePlace?.id ?? ""}}),
    enabled: open && !!storagePlace?.id,
  });

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [prevStoragePlaceId, setPrevStoragePlaceId] = useState(storagePlace?.id);
  if (prevStoragePlaceId !== storagePlace?.id) {
    setPrevStoragePlaceId(storagePlace?.id);
    setSelectedNodeId(null);
  }

  const handleClose = () => {
    onClose();
    setSelectedNodeId(null);
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
      return parts.join(" - ");
    };
    openPrintPage(
      nodes.map((x) => ({
        type: "DataMatrix",
        value: x.id,
        label: `${storagePlace?.name} - ${buildPath(x)}`,
      })),
    );
  };

  return (
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
        sx={{alignItems: "center", justifyContent: "space-between", px: 2, py: 1.5, flexShrink: 0}}
      >
        <Typography variant="h6" noWrap sx={{flex: 1, mr: 1}}>
          {storagePlace?.name ?? ""}
        </Typography>
        <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
          <Button
            size="small"
            startIcon={<PrintIcon />}
            disabled={isLoading || nodes.length === 0}
            onClick={printLabels}
          >
            Этикетки
          </Button>
          <IconButton onClick={handleClose} size="small">
            <CloseIcon />
          </IconButton>
        </Stack>
      </Stack>
      <Divider />

      <Box sx={{overflowY: "auto", flex: 1, p: 2}}>
        <Stack spacing={2}>
          <Stack spacing={1}>
            <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
              <Typography variant="subtitle2">Ячейки</Typography>
              {nodes.length > 0 && <Chip label={nodes.length} size="small" />}
              {(storagePlace?.totalItemsCount ?? 0) > 0 && (
                <Chip
                  label={`${storagePlace!.totalItemsCount} товаров`}
                  size="small"
                  color="primary"
                  variant="outlined"
                />
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
                <SimpleTreeView
                  selectedItems={selectedNodeId}
                  onSelectedItemsChange={(_e, nodeId) => setSelectedNodeId(nodeId)}
                >
                  {renderNodes(buildRoots(nodes), nodes)}
                </SimpleTreeView>
              </Paper>
            )}
          </Stack>

          {selectedNodeId && storagePlace?.id && (
            <>
              <Divider />
              <NodeDetails
                storagePlaceId={storagePlace.id}
                nodeId={selectedNodeId}
                warehouseId={warehouseId}
              />
            </>
          )}
        </Stack>
      </Box>
    </Drawer>
  );
}

export default StoragePlaceDialog;
