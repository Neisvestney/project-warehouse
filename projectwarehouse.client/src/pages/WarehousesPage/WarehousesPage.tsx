import {
  Button,
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink, useNavigate} from "react-router";
import {warehousesGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import RefreshIcon from "@mui/icons-material/Refresh";
import AddIcon from "@mui/icons-material/Add";
import SearchInput from "@/components/SearchInput.tsx";
import DataTableContainer from "@/components/DataTableContainer.tsx";
import TableRowLoader from "@/components/TableRowLoader.tsx";
import TableRowEmpty from "@/components/TableRowEmpty.tsx";

function WarehousesPage() {
  const navigate = useNavigate();
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

  const {data, isLoading, isFetching, refetch} = useQuery(
    warehousesGetAllOptions({query: fetchParams}),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Склады", link: "/warehouses"}, {name: "Список"}]} />
      <PageGenericHeader
        title={"Склады"}
        right={
          <>
            <IconButton color={"inherit"} onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
            <Button
              variant="outlined"
              endIcon={<AddIcon />}
              component={RouterLink}
              to="/warehouses/new"
            >
              Создать
            </Button>
          </>
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
              <TableCell>Мест хранения</TableCell>
              <TableCell>Товаров</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={3} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={3} message="Склады не найдены" />
            ) : (
              data?.items.map((warehouse) => (
                <TableRow
                  key={warehouse.id}
                  hover
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/warehouses/${warehouse.id}`)}
                >
                  <TableCell>{warehouse.name}</TableCell>
                  <TableCell>
                    {warehouse.storagePlaceCount > 0 ? (
                      <Chip label={warehouse.storagePlaceCount} size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    {warehouse.totalItemsCount > 0 ? (
                      <Chip
                        label={warehouse.totalItemsCount}
                        size="small"
                        color="primary"
                        variant="outlined"
                      />
                    ) : (
                      "—"
                    )}
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

export default WarehousesPage;
