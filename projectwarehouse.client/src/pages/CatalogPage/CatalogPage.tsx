import {
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {useNavigate, useSearchParams} from "react-router";
import {catalogGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import SearchInput from "@/components/SearchInput.tsx";
import DataTableContainer from "@/components/DataTableContainer.tsx";
import TableRowLoader from "@/components/TableRowLoader.tsx";
import TableRowEmpty from "@/components/TableRowEmpty.tsx";
import {CatalogItemDrawer} from "./CatalogItemDrawer";

function CatalogPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const selectedItemId = searchParams.get("item");

  const openDrawer = (id: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("item", id);
    navigate(`?${next.toString()}`);
  };

  const closeDrawer = () => {
    const next = new URLSearchParams(searchParams);
    next.delete("item");
    navigate(`?${next.toString()}`, {replace: true});
  };

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
    catalogGetAllOptions({query: fetchParams}),
  );

  return (
    <>
      <Stack spacing={2}>
        <AppBreadcrumbs path={[{name: "Каталог", link: "/catalog"}, {name: "Список"}]} />
        <PageGenericHeader
          title="Каталог"
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
                <TableCell>Штрихкод</TableCell>
                <TableCell>Характеристики</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={4} />
              ) : data?.items.length === 0 ? (
                <TableRowEmpty colSpan={4} message="Позиции не найдены" />
              ) : (
                data?.items.map((item) => (
                  <TableRow
                    key={item.id}
                    hover
                    selected={item.id === selectedItemId}
                    sx={{
                      cursor: "pointer",
                      opacity: isFetching && !isLoading ? 0.5 : 1,
                      transition: "opacity 0.2s",
                    }}
                    onClick={() => openDrawer(item.id)}
                  >
                    <TableCell>{item.name}</TableCell>
                    <TableCell>{item.article}</TableCell>
                    <TableCell>{item.barcode ?? "—"}</TableCell>
                    <TableCell>
                      {item.characteristicCount > 0 ? (
                        <Chip label={item.characteristicCount} size="small" />
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

      <CatalogItemDrawer itemId={selectedItemId} onClose={closeDrawer} />
    </>
  );
}

export default CatalogPage;
