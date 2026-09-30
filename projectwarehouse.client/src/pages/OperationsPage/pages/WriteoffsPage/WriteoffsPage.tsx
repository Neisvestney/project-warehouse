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
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {
  writeoffsBatchTransitionMutation,
  writeoffsBatchUpdateTagsMutation,
  writeoffsGetAllOptions,
  writeoffsGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import {useSelectedItems} from "@/hooks/useSelectedItems";
import BulkBar from "@/components/BulkBar";
import SelectionTableCell from "@/components/SelectionTableCell";
import {useDocumentBulkTransitions} from "@/components/useDocumentBulkTransitions";
import {useBulkTagsAction} from "@/components/tags/useBulkTagsAction";
import {WRITEOFF_BULK_TRANSITIONS} from "@/components/writeoffs/writeoffBulkTransitions";
import {NOUNS} from "@/utils/pluralUtils";
import {byOperation} from "@/utils/queryKeys";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useHasPermission} from "@/hooks/usePermission";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchWithItemsInput from "@/components/catalog/SearchWithItemsInput";
import FiltersBar from "@/components/FiltersBar";
import StatusTabs from "@/components/StatusTabs";
import DataTableContainer from "@/components/DataTableContainer";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import WarehousesSelect from "@/components/WarehousesSelect";
import WriteoffStatusChip from "@/components/writeoffs/WriteoffStatusChip";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import TagChips from "@/components/tags/TagChips";
import {
  WRITEOFF_REASON_LABELS,
  WRITEOFF_REASONS,
  WRITEOFF_STATUS_LABELS,
  formatWriteoffNumber,
} from "@/components/writeoffs/writeoffUtils";
import type {
  WriteoffReason,
  WriteoffSortBy,
  WriteoffStatus,
  WriteoffSummaryDto,
} from "@/api/types.gen";

const SORT_COLUMNS: {key: WriteoffSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "name", label: "Название"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "createdAt", label: "Создано"},
];

const ALL_STATUSES: WriteoffStatus[] = ["draft", "finished", "canceled"];

const getWriteoffId = (writeoff: WriteoffSummaryDto) => writeoff.id;
function WriteoffsPage() {
  const queryClient = useQueryClient();
  const canCreate = useHasPermission(["writeoffs.edit", "writeoffs.edit_assigned"]);

  const [searchString, setSearchString] = useSyncedWithQueryState(
    "search",
    (q) => (typeof q === "string" ? q : ""),
    (v) => v || null,
  );

  const [warehouseId, setWarehouseId] = useSyncedWithQueryState(
    "warehouse",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const [status, setStatus] = useSyncedWithQueryState<WriteoffStatus | "">(
    "status",
    (q) => (ALL_STATUSES.includes(q as WriteoffStatus) ? (q as WriteoffStatus) : ""),
    (v) => v || null,
  );

  const [reason, setReason] = useSyncedWithQueryState<WriteoffReason | "">(
    "reason",
    (q) => (WRITEOFF_REASONS.includes(q as WriteoffReason) ? (q as WriteoffReason) : ""),
    (v) => v || null,
  );

  const [catalogItemIds, setCatalogItemIds] = useSyncedWithQueryState<string[]>(
    "item",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "number", {
    defaultSortOrder: "desc",
  });

  // searchString is already debounced by the search field, so it goes with the immediate params
  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {},
    [],
    {
      searchString: searchString || undefined,
      warehouseId: warehouseId ?? undefined,
      status: (status as WriteoffStatus) || undefined,
      reason: (reason as WriteoffReason) || undefined,
      catalogItemIds: catalogItemIds.length > 0 ? catalogItemIds : undefined,
      tagIds: tagIds.length > 0 ? tagIds : undefined,
      sortBy,
      sortOrder,
    },
    [searchString, warehouseId, status, reason, catalogItemIds, tagIds, sortBy, sortOrder],
  );

  // sortable columns, the fixed ones after them and the checkbox for editors
  const columnCount = SORT_COLUMNS.length + 3 + (canCreate ? 1 : 0);

  const {data, isLoading, isFetching, refetch} = useQuery(
    writeoffsGetAllOptions({query: fetchParams}),
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
  } = useSelectedItems(getWriteoffId, data?.items);

  const invalidateWriteoffs = () =>
    Promise.all([
      queryClient.invalidateQueries({queryKey: writeoffsGetAllQueryKey()}),
      queryClient.invalidateQueries({queryKey: byOperation("writeoffsGetById")}),
    ]);

  const transitions = useDocumentBulkTransitions({
    entity: "writeoff",
    transitions: WRITEOFF_BULK_TRANSITIONS,
    selectedItems,
    enabled: canCreate,
    mutation: writeoffsBatchTransitionMutation(),
    invalidate: invalidateWriteoffs,
    onTransitioned: removeIds,
    noun: NOUNS.writeoff,
    formatNumber: formatWriteoffNumber,
  });

  const tagsAction = useBulkTagsAction({
    kind: "writeoff",
    entity: "writeoff",
    mutation: writeoffsBatchUpdateTagsMutation(),
    invalidate: invalidateWriteoffs,
    noun: NOUNS.writeoff,
    notFound: {
      code: "writeoffNotFound",
      numbersArg: "writeoffNumbers",
      formatNumber: formatWriteoffNumber,
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
        path={[{name: "Списания", link: "/operations/writeoffs"}, {name: "Список"}]}
      />
      <PageGenericHeader
        title="Списания"
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
                to="/operations/writeoffs/new"
              >
                Новое списание
              </Button>
            )}
          </>
        }
      >
        <SearchWithItemsInput
          text={searchString}
          onTextChange={setSearchString}
          itemIds={catalogItemIds}
          onItemIdsChange={setCatalogItemIds}
          sx={{flexGrow: 1}}
        />
      </PageGenericHeader>
      <StatusTabs
        value={status}
        onChange={setStatus}
        statuses={ALL_STATUSES}
        labels={WRITEOFF_STATUS_LABELS}
        counts={statusCounts ?? undefined}
      />
      <FiltersBar activeCount={[warehouseId, reason, tagIds.length > 0].filter(Boolean).length}>
        <WarehousesSelect
          value={warehouseId}
          onChange={setWarehouseId}
          sx={{flexBasis: 200}}
          size="small"
          textFieldProps={{label: "Склад"}}
        />
        <Select
          value={reason}
          onChange={(e) => setReason(e.target.value as WriteoffReason | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все причины</MenuItem>
          {WRITEOFF_REASONS.map((r) => (
            <MenuItem key={r} value={r}>
              {WRITEOFF_REASON_LABELS[r]}
            </MenuItem>
          ))}
        </Select>
        <DocumentTagsFilter
          kind="writeoff"
          value={tagIds}
          onChange={setTagIds}
          sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
        />
      </FiltersBar>
      <BulkBar
        count={selectedItems.length}
        countLabel={{one: "списание выбрано", few: "списания выбрано", many: "списаний выбрано"}}
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
              <TableCell>Причина</TableCell>
              <TableCell>Позиций</TableCell>
              <TableCell>Теги</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={columnCount} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={columnCount} message="Списания не найдены" />
            ) : (
              data?.items.map((writeoff) => (
                <LinkTableRow
                  key={writeoff.id}
                  to={`/operations/writeoffs/${writeoff.id}`}
                  ariaLabel={`Списание ${formatWriteoffNumber(writeoff.number)}`}
                  selected={isSelected(writeoff.id)}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  {canCreate && (
                    <SelectionTableCell
                      checked={isSelected(writeoff.id)}
                      onCheck={(extendRange) => toggle(writeoff, extendRange)}
                    />
                  )}
                  <TableCell sx={{fontFamily: "monospace"}}>
                    {formatWriteoffNumber(writeoff.number)}
                  </TableCell>
                  <TableCell>{writeoff.name || "—"}</TableCell>
                  <TableCell>
                    <WriteoffStatusChip status={writeoff.status} />
                  </TableCell>
                  <TableCell>{writeoff.warehouseName}</TableCell>
                  <TableCell>{new Date(writeoff.createdAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell>{WRITEOFF_REASON_LABELS[writeoff.reason]}</TableCell>
                  <TableCell>
                    {writeoff.itemsCount > 0 ? (
                      <Chip label={writeoff.itemsCount} size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    <TagChips tags={writeoff.tags} />
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

export default WriteoffsPage;
