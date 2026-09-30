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
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {
  stocktakesBatchTransitionMutation,
  stocktakesBatchUpdateTagsMutation,
  stocktakesGetAllOptions,
  stocktakesGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import {useSelectedItems} from "@/hooks/useSelectedItems";
import BulkBar from "@/components/BulkBar";
import SelectionTableCell from "@/components/SelectionTableCell";
import {useDocumentBulkTransitions} from "@/components/useDocumentBulkTransitions";
import {useBulkTagsAction} from "@/components/tags/useBulkTagsAction";
import {STOCKTAKE_BULK_TRANSITIONS} from "@/components/stocktakes/stocktakeBulkTransitions";
import {NOUNS} from "@/utils/pluralUtils";
import {byOperation} from "@/utils/queryKeys";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import FiltersBar from "@/components/FiltersBar";
import StatusTabs from "@/components/StatusTabs";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import WarehousesSelect from "@/components/WarehousesSelect";
import StocktakeStatusChip from "@/components/stocktakes/StocktakeStatusChip";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import TagChips from "@/components/tags/TagChips";
import {
  STOCKTAKE_STATUS_LABELS,
  STOCKTAKE_TYPE_LABELS,
  formatStocktakeNumber,
} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeSortBy, StocktakeStatus, StocktakeSummaryDto} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";

const SORT_COLUMNS: {key: StocktakeSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "name", label: "Название"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "createdAt", label: "Создано"},
];

const ALL_STATUSES: StocktakeStatus[] = ["planned", "draft", "inProgress", "finished", "canceled"];

const getStocktakeId = (stocktake: StocktakeSummaryDto) => stocktake.id;

function StocktakesPage() {
  const queryClient = useQueryClient();
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

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
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
      tagIds: tagIds.length > 0 ? tagIds : undefined,
      sortBy,
      sortOrder,
    },
    [warehouseId, status, tagIds, sortBy, sortOrder],
  );

  // sortable columns, the fixed ones after them and the checkbox for editors
  const columnCount = SORT_COLUMNS.length + 4 + (canCreate ? 1 : 0);

  const {data, isLoading, isFetching, refetch} = useQuery(
    stocktakesGetAllOptions({query: fetchParams}),
  );
  // the tabs keep their last numbers through a refetch instead of collapsing and shifting
  const [statusCounts] = useRetainedValue(data?.meta.statusCounts);

  const {
    selectedItems,
    isSelected,
    allPageSelected,
    somePageSelected,
    toggle,
    toggleAll,
    removeIds,
    clear,
  } = useSelectedItems(getStocktakeId, data?.items);

  const invalidateStocktakes = () =>
    Promise.all([
      queryClient.invalidateQueries({queryKey: stocktakesGetAllQueryKey()}),
      queryClient.invalidateQueries({queryKey: byOperation("stocktakesGetById")}),
    ]);

  const transitions = useDocumentBulkTransitions({
    entity: "stocktake",
    transitions: STOCKTAKE_BULK_TRANSITIONS,
    selectedItems,
    enabled: canCreate,
    mutation: stocktakesBatchTransitionMutation(),
    invalidate: invalidateStocktakes,
    onTransitioned: removeIds,
    noun: NOUNS.stocktake,
    formatNumber: formatStocktakeNumber,
  });

  const tagsAction = useBulkTagsAction({
    kind: "stocktake",
    entity: "stocktake",
    mutation: stocktakesBatchUpdateTagsMutation(),
    invalidate: invalidateStocktakes,
    noun: NOUNS.stocktake,
    notFound: {
      code: "stocktakeNotFound",
      numbersArg: "stocktakeNumbers",
      formatNumber: formatStocktakeNumber,
    },
  });

  const selectionActions =
    selectedItems.length > 0
      ? [
          ...transitions.actions,
          ...(canCreate ? [tagsAction.getAction(selectedItems.map((d) => d.id))] : []),
        ]
      : [];

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
      <StatusTabs
        value={status}
        onChange={setStatus}
        statuses={ALL_STATUSES}
        labels={STOCKTAKE_STATUS_LABELS}
        counts={statusCounts ?? undefined}
      />
      <FiltersBar activeCount={[warehouseId, tagIds.length > 0].filter(Boolean).length}>
        <WarehousesSelect
          value={warehouseId}
          onChange={setWarehouseId}
          sx={{flexBasis: 200}}
          size="small"
          textFieldProps={{label: "Склад"}}
        />
        <DocumentTagsFilter
          kind="stocktake"
          value={tagIds}
          onChange={setTagIds}
          sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
        />
      </FiltersBar>
      <BulkBar
        count={selectedItems.length}
        countLabel={{
          one: "инвентаризация выбрана",
          few: "инвентаризации выбрано",
          many: "инвентаризаций выбрано",
        }}
        onClear={clear}
        actions={selectionActions}
        info={[{key: "total", label: "Всего:", value: (data?.total ?? 0).toLocaleString("ru-RU")}]}
        infoLoading={isLoading}
      />
      {transitions.dialogs}
      {tagsAction.dialogs}
      {transitions.alerts}
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
              {canCreate && (
                <SelectionTableCell
                  checked={allPageSelected}
                  indeterminate={!allPageSelected && somePageSelected}
                  onCheck={() => toggleAll()}
                />
              )}
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
              <TableCell>Теги</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={columnCount} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={columnCount} message="Инвентаризации не найдены" />
            ) : (
              data?.items.map((stocktake) => (
                <LinkTableRow
                  key={stocktake.id}
                  to={`/operations/stocktakes/${stocktake.id}`}
                  ariaLabel={`Инвентаризация ${formatStocktakeNumber(stocktake.number)}`}
                  selected={isSelected(stocktake.id)}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  {canCreate && (
                    <SelectionTableCell
                      checked={isSelected(stocktake.id)}
                      onCheck={(extendRange) => toggle(stocktake, extendRange)}
                    />
                  )}
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
                  <TableCell>
                    <TagChips tags={stocktake.tags} />
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
