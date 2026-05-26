import {useState} from "react";
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
  TableSortLabel,
  Typography,
} from "@mui/material";
import ArchiveIcon from "@mui/icons-material/Archive";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {catalogGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {type CatalogSortBy} from "@/api/types.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useTableSort} from "@/hooks/useTableSort";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import SearchInput from "@/components/SearchInput.tsx";
import DataTableContainer from "@/components/DataTableContainer.tsx";
import TableRowLoader from "@/components/TableRowLoader.tsx";
import TableRowEmpty from "@/components/TableRowEmpty.tsx";
import {CatalogItemDrawer} from "@/components/catalog/CatalogItemDrawer";
import {CreateCatalogItemDialog} from "@/components/catalog/CreateCatalogItemDialog";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState.ts";
import {useHasPermission} from "@/hooks/usePermission";
import AddIcon from "@mui/icons-material/Add";

const SORTABLE_COLUMNS: {key: CatalogSortBy; label: string}[] = [
  {key: "type", label: "Тип"},
  {key: "name", label: "Название"},
  {key: "article", label: "Артикул"},
  {key: "barcode", label: "Штрихкод"},
];

function CatalogPage() {
  const [selectedItemId, openDrawer, closeDrawer] = useDrawerSearchParamsState("item");
  const [createOpen, setCreateOpen] = useState(false);
  const canEdit = useHasPermission("catalog.edit");

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORTABLE_COLUMNS, "name");

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {searchString, sortBy, sortOrder},
    [searchString, sortBy, sortOrder],
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
            <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
              <IconButton color="inherit" onClick={() => refetch()}>
                <RefreshIcon />
              </IconButton>
              {canEdit && (
                <Button endIcon={<AddIcon/>} variant="outlined" size="small" onClick={() => setCreateOpen(true)}>
                  Создать
                </Button>
              )}
            </Stack>
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
                {SORTABLE_COLUMNS.map(({key, label}) => (
                  <TableCell key={key}>
                    <TableSortLabel
                      active={sortBy === key}
                      direction={sortBy === key ? sortOrder : "asc"}
                      onClick={() => handleSortClick(key)}
                    >
                      {label}
                    </TableSortLabel>
                  </TableCell>
                ))}
                <TableCell>Теги</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={5} />
              ) : data?.items.length === 0 ? (
                <TableRowEmpty colSpan={5} message="Позиции не найдены" />
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
                    <TableCell sx={{width: 120}}>
                      <CatalogItemTypeChip type={item.type} />
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
                        <Typography variant="body2">{item.fullName}</Typography>
                        {item.isArchived && (
                          <ArchiveIcon sx={{fontSize: 14, color: "warning.main", flexShrink: 0}} />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell>{item.article}</TableCell>
                    <TableCell>{item.barcode ?? "—"}</TableCell>
                    <TableCell>
                      {item.tags.length > 0 && (
                        <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
                          {item.tags.map((tag) => (
                            <Chip key={tag.id} label={tag.name} size="small" variant="outlined" />
                          ))}
                        </Stack>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </DataTableContainer>
      </Stack>

      <CatalogItemDrawer itemId={selectedItemId} onClose={closeDrawer} onOpenItem={openDrawer} />

      <CreateCatalogItemDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(id) => {
          setCreateOpen(false);
          openDrawer(id);
        }}
      />
    </>
  );
}

export default CatalogPage;
