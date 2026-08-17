import {useState, useCallback, useMemo} from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Divider,
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
  warehousesGetByIdForPrintOptions,
  warehousesGetByIdOptions,
  warehousesGetDefaultNodeOptions,
} from "@/api/@tanstack/react-query.gen";
import StoragePlaceNodeTree from "@/features/warehouse/StoragePlaceNodeTree";
import WarehouseCanvas from "@/features/warehouse/WarehouseCanvas";
import ScannerBlock from "@/components/ScannerBlock/ScannerBlock";
import {useHardwareScanner} from "@/hooks/useHardwareScanner";
import {parseEntityBarcode} from "@/utils/barcodeUtils";
import {buildNodePath, formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

export type {SelectedNode};

type TabId = "canvas" | "schema" | "camera" | "hardware";

interface StorageNodePickerContentProps {
  warehouseId: string;
  onSelect: (node: SelectedNode) => void;
  /** Controls hardware scanner activation — pass the dialog's `open` state. */
  open: boolean;
  catalogItemId?: string;
}

function StorageNodePickerContent({
  warehouseId,
  onSelect,
  open,
  catalogItemId,
}: StorageNodePickerContentProps) {
  const [activeTab, setActiveTab] = useState<TabId>("canvas");
  const [selectedStoragePlaceId, setSelectedStoragePlaceId] = useState<string>("");
  const [scanError, setScanError] = useState<string | null>(null);
  const [scanKey, setScanKey] = useState(0);

  const warehouseQuery = useQuery({
    ...warehousesGetByIdOptions({path: {id: warehouseId}}),
    enabled: open,
    meta: {suppressGlobalError: true},
  });

  const storagePlaces = useMemo(
    () => warehouseQuery.data?.storagePlaces ?? [],
    [warehouseQuery.data],
  );
  const effectiveStoragePlaceId = selectedStoragePlaceId || storagePlaces[0]?.id || "";
  const selectedStoragePlace = storagePlaces.find((sp) => sp.id === effectiveStoragePlaceId);

  const nodesQuery = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: effectiveStoragePlaceId}, query: {catalogItemId}}),
    enabled: open && !!effectiveStoragePlaceId,
    meta: {suppressGlobalError: true},
  });

  const nodes = useMemo(() => nodesQuery.data ?? [], [nodesQuery.data]);

  const parentNodeIds = useMemo(
    () => new Set(nodes.map((n) => n.parentNodeId).filter(Boolean) as string[]),
    [nodes],
  );

  const {
    data: allNodes,
    isError: allNodesIsError,
    refetch: refetchAllNodes,
  } = useQuery({
    ...warehousesGetByIdForPrintOptions({path: {id: warehouseId}}),
    enabled: open,
    meta: {suppressGlobalError: true},
  });

  const handleSelectByBarcode = useCallback(
    (barcode: string) => {
      const failScan = (message: string) => {
        setScanError(message);
        setScanKey((k) => k + 1);
      };

      const parsed = parseEntityBarcode(barcode);
      if (!parsed) {
        failScan(`Неизвестный штрих-код: ${barcode.trim()}`);
        return;
      }
      if (parsed.entity !== "storagePlaceNode") {
        failScan("Этот штрих-код не относится к ячейке хранения");
        return;
      }

      if (!allNodes) {
        if (allNodesIsError) {
          void refetchAllNodes();
          failScan("Не удалось загрузить ячейки склада, повторите сканирование");
        } else {
          failScan("Ячейки склада ещё загружаются, повторите сканирование");
        }
        return;
      }

      const found = allNodes.find((n) => n.id === parsed.id);
      if (!found) {
        failScan(`Ячейка не найдена: ${parsed.id}`);
        return;
      }

      // print DTO carries only the path, so the owning place is resolved by its name (path root)
      const owningPlace = storagePlaces.find((sp) => sp.name === found.name[0]);
      if (owningPlace) setSelectedStoragePlaceId(owningPlace.id);

      onSelect({nodeId: found.id, nodePath: found.name});
      setScanError(null);
    },
    [allNodes, allNodesIsError, refetchAllNodes, storagePlaces, onSelect],
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

  const defaultNodeQuery = useQuery({
    ...warehousesGetDefaultNodeOptions({path: {id: warehouseId}}),
    enabled: open && !!warehouse?.defaultStoragePlaceNodeId,
    meta: {suppressGlobalError: true},
  });
  const defaultNode = defaultNodeQuery.data;

  const handleSelectDefault = () => {
    if (!defaultNode) return;
    onSelect({nodeId: defaultNode.id, nodePath: defaultNode.name});
  };

  if (warehouseQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 4}}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  return (
    <>
      {defaultNode && (
        <>
          <Box sx={{px: 2, py: 1.5}}>
            <Button variant="outlined" size="small" fullWidth onClick={handleSelectDefault}>
              Выбрать «{formatStoragePlaceNodeName(defaultNode.name)}»
            </Button>
          </Box>
          <Divider />
        </>
      )}
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
          {scanError && (
            <Alert severity="error" sx={{mb: 1.5}} onClose={() => setScanError(null)}>
              {scanError}
            </Alert>
          )}

          {activeTab === "schema" &&
            (storagePlaces.length === 0 ? (
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

                <StoragePlaceNodeTree
                  nodes={nodes}
                  isLoading={nodesQuery.isLoading}
                  onSelect={handleNodeSelect}
                />
              </>
            ))}

          {activeTab === "camera" && (
            <Box sx={{height: 300}}>
              <ScannerBlock key={scanKey} onScanned={(barcode) => handleSelectByBarcode(barcode)} />
            </Box>
          )}

          {activeTab === "hardware" && (
            <Box sx={{py: 4, textAlign: "center"}}>
              <Typography color="text.secondary">
                Наведите аппаратный сканер на штрих-код ячейки
              </Typography>
            </Box>
          )}
        </Box>
      )}
    </>
  );
}

export default StorageNodePickerContent;
