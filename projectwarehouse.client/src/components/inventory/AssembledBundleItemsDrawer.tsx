import {
  Box,
  Divider,
  Drawer,
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
import CloseIcon from "@mui/icons-material/Close";
import {useQuery} from "@tanstack/react-query";
import {inventoryItemsGetAllAssembledBundlesOptions} from "@/api/@tanstack/react-query.gen";
import {type AssembledBundleInventoryItemSortBy} from "@/api/types.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useTableSort} from "@/hooks/useTableSort";
import DataTableContainer from "@/components/DataTableContainer";
import SearchInput from "@/components/SearchInput";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";

const SORTABLE_COLUMNS: {key: AssembledBundleInventoryItemSortBy; label: string}[] = [
  {key: "warehouseName", label: "Склад"},
  {key: "storagePlaceName", label: "Место хранения"},
  {key: "nodeName", label: "Ячейка"},
];

interface AssembledBundleItemsDrawerProps {
  catalogItemId: string | null;
  catalogItemName?: string;
  warehouseId?: string;
  storagePlaceId?: string;
  nodeId?: string;
  onClose: () => void;
}

export function AssembledBundleItemsDrawer({
  catalogItemId,
  catalogItemName,
  warehouseId,
  storagePlaceId,
  nodeId,
  onClose,
}: AssembledBundleItemsDrawerProps) {
  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "bundleSearch",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORTABLE_COLUMNS, "warehouseName", {
    sortByParam: "bundleSortBy",
    sortOrderParam: "bundleSortOrder",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      searchString,
      catalogItemId: catalogItemId ?? undefined,
      warehouseId,
      storagePlaceId,
      nodeId,
      sortBy,
      sortOrder,
    },
    [searchString, catalogItemId, warehouseId, storagePlaceId, nodeId, sortBy, sortOrder],
    {defaultPageSize: 10, pageParam: "bundlePage", pageSizeParam: "bundlePageSize"},
  );

  const {data, isLoading, isFetching} = useQuery({
    ...inventoryItemsGetAllAssembledBundlesOptions({query: fetchParams}),
    enabled: !!catalogItemId,
  });

  return (
    <Drawer
      anchor="right"
      open={!!catalogItemId}
      onClose={onClose}
      slotProps={{
        paper: {
          sx: {
            maxWidth: "100vw",
            minWidth: "calc(min(1200px, 100vw))",
            display: "flex",
            flexDirection: "column",
          },
        },
      }}
    >
      <Stack
        direction="row"
        sx={{alignItems: "center", justifyContent: "space-between", px: 2, py: 1.5, flexShrink: 0}}
      >
        <Typography variant="h6" noWrap>
          Сборки{catalogItemName ? ` — ${catalogItemName}` : ""}
        </Typography>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <SearchInput value={inputValue} onChange={setInputValue} size="small" />
          <IconButton size="small" onClick={onClose}>
            <CloseIcon />
          </IconButton>
        </Stack>
      </Stack>
      <Divider />
      <Box sx={{flex: 1, overflow: "auto"}}>
        <DataTableContainer
          isFetching={isFetching}
          count={data?.total ?? 0}
          page={page}
          onPageChange={setPage}
          rowsPerPage={pageSize}
          onRowsPerPageChange={setPageSize}
          rowsPerPageOptions={[10, 20, 50]}
          elevation={0}
        >
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>ID</TableCell>
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
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRowLoader colSpan={4} />
              ) : data?.items.length === 0 ? (
                <TableRowEmpty colSpan={4} message="Сборки не найдены" />
              ) : (
                data?.items.map((item) => (
                  <TableRow key={item.id} hover>
                    <TableCell sx={{fontFamily: "monospace", fontSize: "0.75rem"}}>
                      {item.id.slice(0, 8)}…
                    </TableCell>
                    <TableCell>{item.warehouseName}</TableCell>
                    <TableCell>{item.storagePlaceName}</TableCell>
                    <TableCell>{item.nodeName}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </DataTableContainer>
      </Box>
    </Drawer>
  );
}
