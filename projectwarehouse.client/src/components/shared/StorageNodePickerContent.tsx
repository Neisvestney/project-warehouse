import {useState, useCallback, useMemo} from "react";
import {
  Alert,
  Box,
  CircularProgress,
  MenuItem,
  Select,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import {green} from "@mui/material/colors";
import {useQuery} from "@tanstack/react-query";
import {
  storagePlacesGetNodesOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import StoragePlaceNodeTree from "@/features/warehouse/StoragePlaceNodeTree";
import WarehouseCanvas from "@/features/warehouse/WarehouseCanvas";
import ScannerBlock from "@/components/ScannerBlock/ScannerBlock";
import {useHardwareScanner} from "@/hooks/useHardwareScanner";
import {buildNodePath} from "@/components/shared/nodePathUtils";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

export type {SelectedNode};

type TabId = "canvas" | "schema" | "camera" | "hardware";

interface StorageNodePickerContentProps {
  warehouseId: string;
  onSelect: (node: SelectedNode) => void;
  /** Controls hardware scanner activation — pass the dialog's `open` state. */
  open: boolean;
}

function StorageNodePickerContent({warehouseId, onSelect, open}: StorageNodePickerContentProps) {
  const [activeTab, setActiveTab] = useState<TabId>("canvas");
  const [selectedStoragePlaceId, setSelectedStoragePlaceId] = useState<string>("");
  const [scanError, setScanError] = useState<string | null>(null);
  const [scanKey, setScanKey] = useState(0);

  const warehouseQuery = useQuery({
    ...warehousesGetByIdOptions({path: {id: warehouseId}}),
    enabled: open,
    meta: {suppressGlobalError: true},
  });

  const storagePlaces = warehouseQuery.data?.storagePlaces ?? [];
  const effectiveStoragePlaceId = selectedStoragePlaceId || storagePlaces[0]?.id || "";
  const selectedStoragePlace = storagePlaces.find((sp) => sp.id === effectiveStoragePlaceId);

  const nodesQuery = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: effectiveStoragePlaceId}}),
    enabled: open && !!effectiveStoragePlaceId,
    meta: {suppressGlobalError: true},
  });

  const nodes = useMemo(() => nodesQuery.data ?? [], [nodesQuery.data]);

  const parentNodeIds = useMemo(
    () => new Set(nodes.map((n) => n.parentNodeId).filter(Boolean) as string[]),
    [nodes],
  );

  const handleSelectByBarcode = useCallback(
    (barcode: string) => {
      const scanned = barcode.trim();
      const found = nodes.find((n) => n.id === scanned);
      if (!found) {
        setScanError(`Ячейка не найдена: ${scanned}`);
        setScanKey((k) => k + 1);
        return;
      }
      const storagePlaceName = selectedStoragePlace?.name ?? "";
      onSelect({
        nodeId: found.id,
        nodePath: buildNodePath(nodes, found.id, storagePlaceName),
      });
      setScanError(null);
    },
    [nodes, selectedStoragePlace, onSelect],
  );

  useHardwareScanner(
    useCallback(
      (e) => {
        if (open) handleSelectByBarcode(e.barcode);
      },
      [open, handleSelectByBarcode],
    ),
  );

  const handleNodeSelect = (nodeId: string) => {
    if (parentNodeIds.has(nodeId)) return;
    const storagePlaceName = selectedStoragePlace?.name ?? "";
    onSelect({
      nodeId,
      nodePath: buildNodePath(nodes, nodeId, storagePlaceName),
    });
  };

  const handleCanvasStoragePlaceClick = (storagePlaceId: string) => {
    setSelectedStoragePlaceId(storagePlaceId);
    setActiveTab("schema");
  };

  const warehouse = warehouseQuery.data;

  if (warehouseQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  return (
    <>
      <Tabs
        value={activeTab}
        onChange={(_, v) => {
          setActiveTab(v as TabId);
          setScanError(null);
        }}
        sx={{px: 2, borderBottom: 1, borderColor: "divider"}}
      >
        <Tab label="Карта" value="canvas" />
        <Tab label="Схема" value="schema" />
        <Tab label="Камера" value="camera" />
        <Tab label="Сканер" value="hardware" />
      </Tabs>

      {activeTab === "canvas" && warehouse && (
        <Box sx={{height: 360, position: "relative"}}>
          <WarehouseCanvas
            width={warehouse.width}
            height={warehouse.height}
            layoutObjects={warehouse.layoutObjects}
            storagePlaces={warehouse.storagePlaces.map((sp) => ({
              ...sp,
              fill: green[300],
            }))}
            onStoragePlaceClick={handleCanvasStoragePlaceClick}
          />
        </Box>
      )}

      {activeTab !== "canvas" && (
        <Box sx={{px: 2, pt: 1.5, pb: 1}}>
          {storagePlaces.length === 0 ? (
            <Typography color="text.secondary">Нет мест хранения</Typography>
          ) : (
            <>
              <Select
                value={effectiveStoragePlaceId}
                onChange={(e) => setSelectedStoragePlaceId(e.target.value)}
                size="small"
                fullWidth
                sx={{mb: 1.5}}
              >
                {storagePlaces.map((sp) => (
                  <MenuItem key={sp.id} value={sp.id}>
                    {sp.name}
                  </MenuItem>
                ))}
              </Select>

              {scanError && (
                <Alert severity="error" sx={{mb: 1.5}} onClose={() => setScanError(null)}>
                  {scanError}
                </Alert>
              )}

              {activeTab === "schema" && (
                <StoragePlaceNodeTree
                  nodes={nodes}
                  isLoading={nodesQuery.isLoading}
                  onSelect={handleNodeSelect}
                />
              )}

              {activeTab === "camera" && (
                <Box sx={{height: 300}}>
                  <ScannerBlock
                    key={scanKey}
                    onScanned={(barcode) => handleSelectByBarcode(barcode)}
                  />
                </Box>
              )}

              {activeTab === "hardware" && (
                <Box sx={{py: 4, textAlign: "center"}}>
                  <Typography color="text.secondary">
                    Наведите аппаратный сканер на штрих-код ячейки
                  </Typography>
                </Box>
              )}
            </>
          )}
        </Box>
      )}
    </>
  );
}

export default StorageNodePickerContent;
