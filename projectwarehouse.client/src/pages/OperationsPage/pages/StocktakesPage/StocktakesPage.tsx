import {
  Button,
  Chip,
  IconButton,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {stocktakesGetAllOptions} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import FiltersBar from "@/components/FiltersBar";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import WarehousesSelect from "@/components/WarehousesSelect";
import StocktakeStatusChip from "@/components/stocktakes/StocktakeStatusChip";
import {
  STOCKTAKE_STATUS_LABELS,
  STOCKTAKE_TYPE_LABELS,
  formatStocktakeNumber,
} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeSortBy, StocktakeStatus} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";

const SORT_COLUMNS: {key: StocktakeSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "name", label: "Название"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "createdAt", label: "Создано"},
];

const ALL_STATUSES: StocktakeStatus[] = ["planned", "draft", "inProgress", "finished", "canceled"];

function StocktakesPage() {
  const canCreate = useHasPermission(["stocktakes.edit", "stocktakes.edit_assigned"]);

  const [inputValue, setInputValue, searchString] = useDebouncedSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [warehouseId, setWarehouseId] = useSyncedWithQueryState(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const [status, setStatus] = useSyncedWithQueryState<StocktakeStatus | "">(
    "status",
    (q) => (ALL_STATUSES.includes(q as StocktakeStatus) ? (q as StocktakeStatus) : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "number", {
    defaultSortOrder: "desc",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      warehouseId: warehouseId ?? undefined,
      status: (status as StocktakeStatus) || undefined,
      sortBy,
      sortOrder,
    },
    [warehouseId, status, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    stocktakesGetAllOptions({query: fetchParams}),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Инвентаризации", link: "/operations/stocktakes"}, {name: "Список"}]}
      />
      <PageGenericHeader
        title="Инвентаризации"
        refresh={
          <IconButton color="inherit" onClick={() => refetch()}>
            <RefreshIcon />
          </IconButton>
        }
        actions={
          <>
            {canCreate && (
              <Button
                variant="outlined"
                endIcon={<AddIcon />}
                size="small"
                component={RouterLink}
                to="/operations/stocktakes/new"
              >
                Новая инвентаризация
              </Button>
            )}
          </>
        }
      >
        <SearchInput value={inputValue} onChange={setInputValue} />
      </PageGenericHeader>
      <FiltersBar>
        <WarehousesSelect
          value={warehouseId}
          onChange={setWarehouseId}
          sx={{flexBasis: 200}}
          size="small"
          textFieldProps={{label: "Склад"}}
        />
        <Select
          value={status}
          onChange={(e) => setStatus(e.target.value as StocktakeStatus | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все статусы</MenuItem>
          {ALL_STATUSES.map((s) => (
            <MenuItem key={s} value={s}>
              {STOCKTAKE_STATUS_LABELS[s]}
            </MenuItem>
          ))}
        </Select>
      </FiltersBar>
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
              {SORT_COLUMNS.map(({key, label}) => (
                <TableCell key={key} sortDirection={sortBy === key ? sortOrder : false}>
                  <TableSortLabel
                    active={sortBy === key}
                    direction={sortBy === key ? sortOrder : "asc"}
                    onClick={() => handleSortClick(key)}
                  >
                    {label}
                  </TableSortLabel>
                </TableCell>
              ))}
              <TableCell>Тип</TableCell>
              <TableCell>Ячеек</TableCell>
              <TableCell>Позиций</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Инвентаризации не найдены" />
            ) : (
              data?.items.map((stocktake) => (
                <LinkTableRow
                  key={stocktake.id}
                  to={`/operations/stocktakes/${stocktake.id}`}
                  ariaLabel={`Инвентаризация ${formatStocktakeNumber(stocktake.number)}`}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  <TableCell sx={{fontFamily: "monospace"}}>
                    {formatStocktakeNumber(stocktake.number)}
                  </TableCell>
                  <TableCell>{stocktake.name || "—"}</TableCell>
                  <TableCell>
                    <StocktakeStatusChip status={stocktake.status} />
                  </TableCell>
                  <TableCell>{stocktake.warehouseName}</TableCell>
                  <TableCell>{new Date(stocktake.createdAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell>
                    {STOCKTAKE_TYPE_LABELS[stocktake.type]}
                    {stocktake.plannedDate && (
                      <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
                        {parseDateOnly(stocktake.plannedDate).toLocaleDateString("ru-RU")}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    {stocktake.nodesCount > 0 ? (
                      <Chip label={stocktake.nodesCount} size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    {stocktake.itemsCount > 0 ? (
                      <Chip label={stocktake.itemsCount} size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                </LinkTableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default StocktakesPage;
