import {Box, CircularProgress, Stack} from "@mui/material";
import {useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {
  storagePlacesGetNodesOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {isNotFoundError} from "@/utils/errorUtils";
import ItemsBasePage from "@/components/inventory/ItemsBasePage";

function NodeInventoryPage() {
  const {warehouseId, storagePlaceId, nodeId} = useParams<{
    warehouseId: string;
    storagePlaceId: string;
    nodeId: string;
  }>();

  const {
    data: warehouse,
    isLoading: warehouseLoading,
    isError: warehouseError,
    isRefetchError: warehouseRefetchError,
    error: warehouseErr,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: warehouseId!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const {
    data: nodes,
    isLoading: nodesLoading,
    isError: nodesError,
    isRefetchError: nodesRefetchError,
    error: nodesErr,
  } = useQuery({
    ...storagePlacesGetNodesOptions({path: {id: storagePlaceId!}}),
    enabled: !!storagePlaceId,
    meta: {suppressGlobalError: true},
  });

  const isLoading = warehouseLoading || nodesLoading;

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (warehouseError && !warehouseRefetchError)
    return isNotFoundError(warehouseErr) ? <NotFound /> : <QueryError error={warehouseErr} />;
  if (!warehouse) return <NotFound />;

  if (nodesError && !nodesRefetchError) return <QueryError error={nodesErr} />;

  const storagePlace = warehouse.storagePlaces.find((sp) => sp.id === storagePlaceId);
  if (!storagePlace) return <NotFound />;

  const node = nodes?.find((n) => n.id === nodeId);
  if (nodes && !node) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Склады", link: "/storage/warehouses"},
          {name: warehouse.name, link: `/storage/warehouses/${warehouseId}`},
          {name: storagePlace.name},
          {name: node?.name ?? "Ячейка"},
          {name: "Остатки"},
        ]}
      />
      <ItemsBasePage
        title={`Остатки — ${node?.name ?? "Ячейка"}`}
        warehouseId={warehouseId}
        storagePlaceId={storagePlaceId}
        nodeId={nodeId}
      />
    </Stack>
  );
}

export default NodeInventoryPage;
