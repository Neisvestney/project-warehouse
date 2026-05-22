import {useState} from "react";
import {useParams} from "react-router";
import {Box, Button, CircularProgress, Divider, Paper, Stack, Typography} from "@mui/material";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {
  inboundOrderProcessingGetByIdOptions,
  inboundOrderProcessingGetStoragePlaceNodeDetailsOptions,
} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError, extractErrorMessage} from "@/utils/errorUtils";
import {IS_DEV} from "@/configuration/flagsConstants";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import MapIcon from "@mui/icons-material/Map";
import LoadingOverlay from "@/components/LoadingOverlay.tsx";
import OrderHeader from "./components/OrderHeader.tsx";
import ManualBarcodeInput from "./components/ManualBarcodeInput.tsx";
import CameraScannerBlock from "./components/CameraScannerBlock.tsx";
import WarehouseSchemaDrawer from "./components/WarehouseSchemaDrawer.tsx";
import NodeDetailsDrawer from "./components/NodeDetailsDrawer.tsx";
import HardwareScannerBlock from "@/pages/InboundOrderProcessingPage/pages/InboundOrderProcessingOrderPage/components/HardwareScannerBlock.tsx";
import {Capacitor} from "@capacitor/core";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState.ts";

function InboundOrderProcessingOrderPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedStoragePlaceName, setSelectedStoragePlaceName] = useState<string | null>(null);
  const [scanError, setScanError] = useState<string | null>(null);
  const [isLookupLoading, setIsLookupLoading] = useState(false);

  const [schemaDrawerOpen, openSchemaDrawer, closeSchemaDrawer] =
    useDrawerSearchParamsState("schemaDrawerOpen");

  const {
    data: order,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...inboundOrderProcessingGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  if (!id) return <NotFound />;

  const handleNodeScanned = async (nodeId: string) => {
    setIsLookupLoading(true);
    setScanError(null);
    try {
      const details = await queryClient.fetchQuery(
        inboundOrderProcessingGetStoragePlaceNodeDetailsOptions({path: {id, nodeId}}),
      );
      const sp = order?.warehouse.storagePlaces.find((s) => s.id === details.storagePlaceId);
      setSelectedStoragePlaceName(sp?.name ?? null);
      setSelectedNodeId(nodeId);
    } catch (err) {
      setScanError(extractErrorMessage(err));
    } finally {
      setIsLookupLoading(false);
    }
  };

  const handleNodeSelectedFromSchema = (nodeId: string, storagePlaceId: string) => {
    const sp = order?.warehouse.storagePlaces.find((s) => s.id === storagePlaceId);
    setSelectedStoragePlaceName(sp?.name ?? null);  
    setSelectedNodeId(nodeId);
  };

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!order) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Обработка приходных ордеров", link: "/inbound-order-processing"},
          {name: `Ордер #${order.number}`},
        ]}
      />

      <OrderHeader order={order} />

      <Paper sx={{p: 2, position: "relative"}}>
        <LoadingOverlay open={isLookupLoading} />
        <Stack spacing={2}>
          <HardwareScannerBlock onNodeScanned={handleNodeScanned} />
          {Capacitor.isNativePlatform() && <Divider />}
          <Typography variant="subtitle2" color="text.secondary">
            Выберите ячейку для размещения товаров
          </Typography>
          <Button
            variant="outlined"
            size="large"
            startIcon={<MapIcon />}
            onClick={() => openSchemaDrawer("true")}
            fullWidth
          >
            Выбрать из схемы склада
          </Button>
          {IS_DEV && <Divider />}
          {IS_DEV && (
            <ManualBarcodeInput
              onNodeScanned={handleNodeScanned}
              isLookupLoading={isLookupLoading}
              lookupError={scanError}
            />
          )}
          {IS_DEV && <Divider />}
          {IS_DEV && <CameraScannerBlock onNodeScanned={handleNodeScanned} />}
        </Stack>
      </Paper>

      <WarehouseSchemaDrawer
        open={!!schemaDrawerOpen}
        onClose={() => closeSchemaDrawer()}
        order={order}
        onNodeSelected={handleNodeSelectedFromSchema}
      />

      <NodeDetailsDrawer
        open={!!selectedNodeId}
        onClose={() => {
          setSelectedNodeId(null);
          setScanError(null);
        }}
        orderId={id}
        nodeId={selectedNodeId}
        storagePlaceName={selectedStoragePlaceName ?? undefined}
      />
    </Stack>
  );
}

export default InboundOrderProcessingOrderPage;
