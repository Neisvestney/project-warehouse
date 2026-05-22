import {useState} from "react";
import {
  Box,
  CircularProgress,
  Divider,
  Drawer,
  IconButton,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {inboundOrderProcessingGetStoragePlaceNodesOptions} from "@/api/@tanstack/react-query.gen.ts";
import {type InboundOrderProcessingDto} from "@/api/types.gen.ts";
import {green} from "@mui/material/colors";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CloseIcon from "@mui/icons-material/Close";
import {WarehouseCanvas, StoragePlaceNodeTree} from "@/features/warehouse";

type DrawerView = "canvas" | "nodeTree";

interface WarehouseSchemaDrawerProps {
  open: boolean;
  onClose: () => void;
  order: InboundOrderProcessingDto;
  onNodeSelected: (nodeId: string, storagePlaceId: string) => void;
}

function WarehouseSchemaDrawer({open, onClose, order, onNodeSelected}: WarehouseSchemaDrawerProps) {
  const [view, setView] = useState<DrawerView>("canvas");
  const [selectedStoragePlaceId, setSelectedStoragePlaceId] = useState<string | null>(null);
  const [expandedItems, setExpandedItems] = useState<string[]>([]);

  const selectedStoragePlace = order.warehouse.storagePlaces.find(
    (s) => s.id === selectedStoragePlaceId,
  );

  const {data: nodes = [], isFetching: nodesLoading} = useQuery({
    ...inboundOrderProcessingGetStoragePlaceNodesOptions({
      path: {id: order.id},
      query: {storagePlaceId: selectedStoragePlaceId ?? undefined},
    }),
    enabled: open && !!selectedStoragePlaceId && view === "nodeTree",
    placeholderData: [],
  });

  const handleStoragePlaceClick = (id: string) => {
    setSelectedStoragePlaceId(id);
    setView("nodeTree");
  };

  const handleNodeSelect = (nodeId: string) => {
    if (!selectedStoragePlaceId) return;
    const hasChildren = nodes.some((n) => n.parentNodeId === nodeId);
    if (hasChildren) {
      return;
    }
    onNodeSelected(nodeId, selectedStoragePlaceId);
    handleClose();
  };

  const handleBack = () => {
    setView("canvas");
    setSelectedStoragePlaceId(null);
    setExpandedItems([]);
  };

  const handleClose = () => {
    onClose();
    setView("canvas");
    setSelectedStoragePlaceId(null);
    setExpandedItems([]);
  };

  const title =
    view === "canvas"
      ? `Схема склада: ${order.warehouse.name}`
      : (selectedStoragePlace?.name ?? "Место хранения");

  return (
    <Drawer
      anchor="bottom"
      open={open}
      onClose={handleClose}
      slotProps={{
        paper: {
          sx: {
            height: "80vh",
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
        <Stack direction="row" spacing={1} sx={{alignItems: "center", flex: 1, minWidth: 0}}>
          {view === "nodeTree" && (
            <IconButton size="small" onClick={handleBack}>
              <ArrowBackIcon />
            </IconButton>
          )}
          <Typography variant="h6" noWrap>
            {title}
          </Typography>
        </Stack>
        <IconButton size="small" onClick={handleClose}>
          <CloseIcon />
        </IconButton>
      </Stack>
      <Divider />

      <Box sx={{flex: 1, overflow: "hidden", position: "relative"}}>
        {view === "canvas" ? (
          <WarehouseCanvas
            width={order.warehouse.width}
            height={order.warehouse.height}
            layoutObjects={order.warehouse.layoutObjects}
            storagePlaces={order.warehouse.storagePlaces.map((p) => ({
              ...p,
              fill: p.hasOrderItems ? green[500] : green[200],
            }))}
            onStoragePlaceClick={handleStoragePlaceClick}
          />
        ) : (
          <Box sx={{p: 2, overflowY: "auto", height: "100%"}}>
            {nodesLoading ? (
              <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
                <CircularProgress />
              </Box>
            ) : (
              <Paper variant="outlined" sx={{p: 1}}>
                <StoragePlaceNodeTree
                  nodes={nodes}
                  onSelect={handleNodeSelect}
                  expandedItems={expandedItems}
                  onExpandedItemsChange={setExpandedItems}
                />
              </Paper>
            )}
          </Box>
        )}
      </Box>
    </Drawer>
  );
}

export default WarehouseSchemaDrawer;
