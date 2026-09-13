import {Stack} from "@mui/material";
import PageLoader from "@/components/PageLoader";
import {useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageTitle from "@/components/PageTitle.tsx";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {isNotFoundError} from "@/utils/errorUtils";
import ItemsBasePage from "@/components/inventory/ItemsBasePage";

function WarehouseInventoryPage() {
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

  if (isLoading) return <PageLoader inline />;

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!warehouse) return <NotFound />;

  return (
    <Stack spacing={2}>
      <PageTitle title={`Остатки — ${warehouse.name}`} />
      <AppBreadcrumbs
        path={[
          {name: "Склады", link: "/storage/warehouses"},
          {name: warehouse.name, link: `/storage/warehouses/${id}`},
          {name: "Остатки"},
        ]}
      />
      <ItemsBasePage title={`Остатки — ${warehouse.name}`} warehouseId={id} />
    </Stack>
  );
}

export default WarehouseInventoryPage;
