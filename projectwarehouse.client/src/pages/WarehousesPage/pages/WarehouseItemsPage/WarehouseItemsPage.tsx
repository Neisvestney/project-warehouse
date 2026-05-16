import {Link, useParams} from "react-router";
import {useQuery} from "@tanstack/react-query";
import {
  warehousesGetAllItemsGroupsOptions,
  warehousesGetByIdOptions,
} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import SearchInput from "@/components/SearchInput";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

function WarehouseItemsPage() {
  const {id} = useParams<{id: string}>();

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString},
    [searchString],
  );

  const {data: warehouse} = useQuery({
    ...warehousesGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const {data, isLoading, isFetching, isError, isRefetchError, error, refetch} = useQuery({
    ...warehousesGetAllItemsGroupsOptions({path: {id: id!}, query: fetchParams}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Склады", link: "/warehouses"},
          {name: warehouse?.name ?? "…", link: `/warehouses/${id}`},
          {name: "Список товаров"},
        ]}
      />
      <PageGenericHeader
        title="Список товаров"
        right={
          <IconButton color="inherit" onClick={() => refetch()}>
            <RefreshIcon />
          </IconButton>
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <DataTableContainer
        isFetching={isFetching}
        count={data?.total ?? 0}
        page={page}
        onPageChange={setPage}
        rowsPerPage={pageSize}
        onRowsPerPageChange={setPageSize}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Название</TableCell>
              <TableCell>Артикул</TableCell>
              <TableCell>Характеристика</TableCell>
              <TableCell>Штрихкод</TableCell>
              <TableCell align="right">Количество</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={6} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={6} message="Товары не найдены" />
            ) : (
              data?.items.map((item) => (
                <TableRow
                  key={item.id}
                  sx={{opacity: isFetching && !isLoading ? 0.5 : 1, transition: "opacity 0.2s"}}
                >
                  <TableCell>{item.catalogItemWithCharacteristic.catalogItem.name}</TableCell>
                  <TableCell>{item.catalogItemWithCharacteristic.catalogItem.article}</TableCell>
                  <TableCell>{item.catalogItemWithCharacteristic.characteristic}</TableCell>
                  <TableCell>
                    {item.catalogItemWithCharacteristic.barcode ??
                      item.catalogItemWithCharacteristic.catalogItem.barcode ??
                      "—"}
                  </TableCell>
                  <TableCell align="right">
                    <Chip label={item.count} size="small" color="primary" variant="outlined" />
                  </TableCell>
                  <TableCell padding="checkbox">
                    <Tooltip title="Открыть в каталоге">
                      <IconButton
                        size="small"
                        component={Link}
                        to={`/catalog?item=${item.catalogItemWithCharacteristic.catalogItem.id}`}
                      >
                        <OpenInNewIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default WarehouseItemsPage;
