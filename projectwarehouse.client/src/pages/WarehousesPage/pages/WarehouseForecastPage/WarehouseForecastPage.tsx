import {Box, CircularProgress, Stack} from "@mui/material";
import {useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {isNotFoundError} from "@/utils/errorUtils";
import ForecastBasePage from "@/components/forecast/ForecastBasePage";

function WarehouseForecastPage() {
  const {id} = useParams<{id: string}>();

  const {
    data: warehouse,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: id!}}),
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

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Склады", link: "/storage/warehouses"},
          {name: warehouse.name, link: `/storage/warehouses/${id}`},
          {name: "Прогноз остатков"},
        ]}
      />
      <ForecastBasePage title={`Прогноз остатков — ${warehouse.name}`} warehouseId={id} />
    </Stack>
  );
}

export default WarehouseForecastPage;
