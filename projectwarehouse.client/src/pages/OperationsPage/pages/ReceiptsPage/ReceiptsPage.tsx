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
  receiptsBatchTransitionMutation,
  receiptsBatchUpdateTagsMutation,
  receiptsGetAllOptions,
  receiptsGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import {useSelectedItems} from "@/hooks/useSelectedItems";
import BulkBar from "@/components/BulkBar";
import SelectionTableCell from "@/components/SelectionTableCell";
import {useDocumentBulkTransitions} from "@/components/useDocumentBulkTransitions";
import {useBulkTagsAction} from "@/components/tags/useBulkTagsAction";
import {RECEIPT_BULK_TRANSITIONS} from "@/components/receipts/receiptBulkTransitions";
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
import ReceiptStatusChip from "@/components/receipts/ReceiptStatusChip";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import {
  RECEIPT_REASON_LABELS,
  RECEIPT_STATUS_LABELS,
  formatReceiptNumber,
} from "@/components/receipts/receiptUtils";
import type {ReceiptReason, ReceiptSortBy, ReceiptStatus, ReceiptSummaryDto} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";

const SORT_COLUMNS: {key: ReceiptSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "name", label: "Название"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "createdAt", label: "Создана"},
  {key: "plannedDeliveryDate", label: "Планируемая дата доставки"},
];

const ALL_STATUSES: ReceiptStatus[] = ["draft", "planned", "processing", "finished", "canceled"];

const ALL_REASONS: ReceiptReason[] = ["newGoods", "return", "other"];

const getReceiptId = (receipt: ReceiptSummaryDto) => receipt.id;

function ReceiptsPage() {
  const queryClient = useQueryClient();
  const canCreate = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);

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

  const [status, setStatus] = useSyncedWithQueryState<ReceiptStatus | "">(
    "status",
    (q) => (ALL_STATUSES.includes(q as ReceiptStatus) ? (q as ReceiptStatus) : ""),
    (v) => v || null,
  );

  const [reason, setReason] = useSyncedWithQueryState<ReceiptReason | "">(
    "reason",
    (q) => (ALL_REASONS.includes(q as ReceiptReason) ? (q as ReceiptReason) : ""),
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
      status: (status as ReceiptStatus) || undefined,
      reason: (reason as ReceiptReason) || undefined,
      catalogItemIds: catalogItemIds.length > 0 ? catalogItemIds : undefined,
      tagIds: tagIds.length > 0 ? tagIds : undefined,
      sortBy,
      sortOrder,
    },
    [searchString, warehouseId, status, reason, catalogItemIds, tagIds, sortBy, sortOrder],
  );

  // sortable columns, the fixed ones after them and the checkbox for editors
  const columnCount = SORT_COLUMNS.length + 4 + (canCreate ? 1 : 0);

  const {data, isLoading, isFetching, refetch} = useQuery(
    receiptsGetAllOptions({query: fetchParams}),
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
  } = useSelectedItems(getReceiptId, data?.items);

  const invalidateReceipts = () =>
    Promise.all([
      queryClient.invalidateQueries({queryKey: receiptsGetAllQueryKey()}),
      queryClient.invalidateQueries({queryKey: byOperation("receiptsGetById")}),
    ]);

  const transitions = useDocumentBulkTransitions({
    entity: "receipt",
    transitions: RECEIPT_BULK_TRANSITIONS,
    selectedItems,
    enabled: canCreate,
    mutation: receiptsBatchTransitionMutation(),
    invalidate: invalidateReceipts,
    onTransitioned: removeIds,
    noun: NOUNS.receipt,
    formatNumber: formatReceiptNumber,
  });

  const tagsAction = useBulkTagsAction({
    kind: "receipt",
    entity: "receipt",
    mutation: receiptsBatchUpdateTagsMutation(),
    invalidate: invalidateReceipts,
    noun: NOUNS.receipt,
    notFound: {
      code: "receiptNotFound",
      numbersArg: "receiptNumbers",
      formatNumber: formatReceiptNumber,
    },
  });

  const selectionActions =
    selectedItems.length > 0
      ? [
          ...transitions.actions,
          ...(canCreate ? [tagsAction.getAction(selectedItems.map((r) => r.id))] : []),
        ]
      : [];

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Приемки", link: "/operations/receipts"}, {name: "Список"}]} />
      <PageGenericHeader
        title="Приемки"
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
                to="/operations/receipts/new"
              >
                Новая приемка
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
        labels={RECEIPT_STATUS_LABELS}
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
          onChange={(e) => setReason(e.target.value as ReceiptReason | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все причины</MenuItem>
          {ALL_REASONS.map((r) => (
            <MenuItem key={r} value={r}>
              {RECEIPT_REASON_LABELS[r]}
            </MenuItem>
          ))}
        </Select>
        <DocumentTagsFilter
          kind="receipt"
          value={tagIds}
          onChange={setTagIds}
          sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
        />
      </FiltersBar>
      <BulkBar
        count={selectedItems.length}
        countLabel={{one: "приемка выбрана", few: "приемки выбрано", many: "приемок выбрано"}}
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
              <TableCell>Запланировано / Принято</TableCell>
              <TableCell>Теги</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={columnCount} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={columnCount} message="Приемки не найдены" />
            ) : (
              data?.items.map((receipt) => (
                <LinkTableRow
                  key={receipt.id}
                  to={`/operations/receipts/${receipt.id}`}
                  ariaLabel={`Приемка ${formatReceiptNumber(receipt.number)}`}
                  selected={isSelected(receipt.id)}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  {canCreate && (
                    <SelectionTableCell
                      checked={isSelected(receipt.id)}
                      onCheck={(extendRange) => toggle(receipt, extendRange)}
                    />
                  )}
                  <TableCell sx={{fontFamily: "monospace"}}>
                    {formatReceiptNumber(receipt.number)}
                  </TableCell>
                  <TableCell>{receipt.name || "—"}</TableCell>
                  <TableCell>
                    <ReceiptStatusChip status={receipt.status} />
                  </TableCell>
                  <TableCell>{receipt.warehouseName}</TableCell>
                  <TableCell>{new Date(receipt.createdAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell>
                    {receipt.plannedDeliveryDate
                      ? parseDateOnly(receipt.plannedDeliveryDate).toLocaleDateString("ru-RU")
                      : "—"}
                  </TableCell>
                  <TableCell>{RECEIPT_REASON_LABELS[receipt.reason]}</TableCell>
                  <TableCell>
                    {receipt.itemsCount > 0 ? (
                      <Chip label={receipt.itemsCount} size="small" />
                    ) : (
                      "—"
                    )}
                  </TableCell>
                  <TableCell>
                    {receipt.totalPlannedCount} / {receipt.totalReceivedCount ?? "—"}
                  </TableCell>
                  <TableCell>
                    {receipt.tags.length > 0 ? (
                      <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap"}}>
                        {receipt.tags.map((tag) => (
                          <Chip key={tag.id} label={tag.name} size="small" />
                        ))}
                      </Stack>
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

export default ReceiptsPage;
