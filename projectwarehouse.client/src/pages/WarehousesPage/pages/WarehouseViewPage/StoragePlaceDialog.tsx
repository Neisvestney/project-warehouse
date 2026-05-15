import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
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

interface StoragePlaceDialogProps {
  open: boolean;
  storagePlace?: StoragePlaceDto;
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
    <TreeItem key={node.id} itemId={node.id} label={node.name}>
      {renderNodes(getChildren(all, node.id), all)}
    </TreeItem>
  ));
}

function StoragePlaceDialog({open, storagePlace, onClose}: StoragePlaceDialogProps) {
  const {data: nodes = [], isLoading} = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: storagePlace?.id ?? ""}}),
    enabled: open && !!storagePlace?.id,
  });

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);

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
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="xs">
      <DialogTitle>Место хранения: {storagePlace?.name}</DialogTitle>
      <DialogContent>
        <Stack spacing={1}>
          <Box>
            <Button disabled={isLoading} onClick={printLabels} variant={"outlined"}>
              Напечатать этикетки
            </Button>
          </Box>
          <Paper sx={{p: 2}}>
            <Typography variant={"overline"}>Ячейки:</Typography>
            {isLoading ? (
              <Stack sx={{alignItems: "center", py: 2}}>
                <CircularProgress size={32} />
              </Stack>
            ) : (
              <SimpleTreeView
                selectedItems={selectedNodeId}
                onSelectedItemsChange={(_e, nodeId) => setSelectedNodeId(nodeId)}
              >
                {renderNodes(buildRoots(nodes), nodes)}
              </SimpleTreeView>
            )}
          </Paper>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Закрыть</Button>
      </DialogActions>
    </Dialog>
  );
}

export default StoragePlaceDialog;
