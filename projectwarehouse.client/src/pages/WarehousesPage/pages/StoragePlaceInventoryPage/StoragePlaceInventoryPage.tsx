import {Box, CircularProgress, Stack} from "@mui/material";
import {useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {isNotFoundError} from "@/utils/errorUtils";
import ItemsBasePage from "@/components/inventory/ItemsBasePage";

function StoragePlaceInventoryPage() {
  const {warehouseId, storagePlaceId} = useParams<{
    warehouseId: string;
    storagePlaceId: string;
  }>();

  const {
    data: warehouse,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: warehouseId!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!warehouse) return <NotFound />;

  const storagePlace = warehouse.storagePlaces.find((sp) => sp.id === storagePlaceId);
  if (!storagePlace) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Склады", link: "/storage/warehouses"},
          {name: warehouse.name, link: `/storage/warehouses/${warehouseId}`},
          {name: storagePlace?.name ?? "Место хранения"},
          {name: "Остатки"},
        ]}
      />
      <ItemsBasePage
        title={`Остатки — ${storagePlace.name}`}
        warehouseId={warehouseId}
        storagePlaceId={storagePlaceId}
      />
    </Stack>
  );
}

export default StoragePlaceInventoryPage;
