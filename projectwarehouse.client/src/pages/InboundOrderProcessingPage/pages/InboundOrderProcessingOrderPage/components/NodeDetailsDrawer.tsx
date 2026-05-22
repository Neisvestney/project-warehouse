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
  Typography,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {
  inboundOrderProcessingGetStoragePlaceNodeDetailsOptions,
  inboundOrderProcessingGetStoragePlaceNodesOptions,
} from "@/api/@tanstack/react-query.gen.ts";
import {type ProcessingStoragePlaceNodeDto} from "@/api/types.gen.ts";
import CloseIcon from "@mui/icons-material/Close";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import ItemsGroupsTable from "./ItemsGroupsTable.tsx";
import EditItemsTable from "./EditItemsTable.tsx";

type EditMode = "place" | "update" | null;

function buildNodePath(
  nodes: ProcessingStoragePlaceNodeDto[],
  targetNodeId: string,
  storagePlaceName?: string,
): string {
  const map = new Map(nodes.map((n) => [n.id, n]));
  const parts: string[] = [];
  let cur = map.get(targetNodeId);
  while (cur) {
    parts.unshift(cur.name);
    cur = cur.parentNodeId ? map.get(cur.parentNodeId) : undefined;
  }
  return [storagePlaceName, ...parts].filter(Boolean).join(" / ");
}

interface NodeDetailsDrawerProps {
  open: boolean;
  onClose: () => void;
  orderId: string;
  nodeId: string | null;
  storagePlaceName?: string;
}

function NodeDetailsDrawer({
  open,
  onClose,
  orderId,
  nodeId,
  storagePlaceName,
}: NodeDetailsDrawerProps) {
  const [editMode, setEditMode] = useState<EditMode>(null);

  const {
    data: details,
    isLoading: detailsLoading,
    isError: detailsError,
  } = useQuery({
    ...inboundOrderProcessingGetStoragePlaceNodeDetailsOptions({
      path: {id: orderId, nodeId: nodeId!},
    }),
    enabled: open && !!nodeId,
    meta: {suppressGlobalError: true},
  });

  const {data: nodes = []} = useQuery({
    ...inboundOrderProcessingGetStoragePlaceNodesOptions({
      path: {id: orderId},
      query: {storagePlaceId: details?.storagePlaceId},
    }),
    enabled: open && !!details?.storagePlaceId,
  });

  const handleClose = () => {
    onClose();
    setEditMode(null);
  };

  const pathTitle =
    nodeId && nodes.length > 0
      ? buildNodePath(nodes, nodeId, storagePlaceName)
      : (storagePlaceName ?? "");

  const hasOrderItems = (details?.orderItemsGroups.length ?? 0) > 0;

  return (
    <Drawer
      anchor="bottom"
      open={open}
      onClose={handleClose}
      slotProps={{
        paper: {
          sx: {
            height: "85vh",
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
        <Typography variant="subtitle1" sx={{fontWeight: 600, flex: 1, mr: 1}} noWrap>
          {pathTitle || "Ячейка"}
        </Typography>
        <IconButton size="small" onClick={handleClose}>
          <CloseIcon />
        </IconButton>
      </Stack>
      <Divider />

      <Box sx={{overflowY: "auto", flex: 1, p: 2}}>
        {detailsLoading ? (
          <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
            <CircularProgress />
          </Box>
        ) : detailsError ? (
          <Alert severity="error">Не удалось загрузить данные ячейки</Alert>
        ) : !details ? null : (
          <Stack spacing={2}>
            {/* Current items section */}
            <Stack spacing={1}>
              <Typography variant="subtitle2">Текущие товары в ячейке</Typography>
              <ItemsGroupsTable groups={details.itemsGroups} emptyMessage="Ячейка пуста" />
            </Stack>

            {/* Order items section (shown when items were placed via this order) */}
            {hasOrderItems && (
              <>
                <Divider />
                <Stack spacing={1}>
                  <Typography variant="subtitle2">Размещено в этом ордере</Typography>
                  <ItemsGroupsTable groups={details.orderItemsGroups} />
                </Stack>
              </>
            )}

            {/* Action buttons */}
            {editMode === null && (
              <>
                <Divider />
                <Stack direction="row" spacing={1} sx={{flexWrap: "wrap"}} useFlexGap>
                  {!hasOrderItems && (
                    <Button
                      variant="contained"
                      size="large"
                      startIcon={<AddIcon />}
                      onClick={() => setEditMode("place")}
                      sx={{flex: 1}}
                    >
                      Добавить
                    </Button>
                  )}
                  <Button
                    variant="outlined"
                    size="large"
                    startIcon={<EditIcon />}
                    onClick={() => setEditMode("update")}
                    sx={{flex: 1}}
                  >
                    Обновить
                  </Button>
                </Stack>
              </>
            )}

            {/* Edit table */}
            {editMode !== null && nodeId && (
              <>
                <Divider />
                <Typography variant="subtitle2">
                  {editMode === "place" ? "Добавление товаров" : "Обновление товаров"}
                </Typography>
                <EditItemsTable
                  orderId={orderId}
                  nodeId={nodeId}
                  mode={editMode}
                  initialItems={editMode === "update" ? details.itemsGroups : []}
                  onSuccess={() => setEditMode(null)}
                  onCancel={() => setEditMode(null)}
                />
              </>
            )}
          </Stack>
        )}
      </Box>
    </Drawer>
  );
}

export default NodeDetailsDrawer;
