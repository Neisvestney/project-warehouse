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
import {useQuery} from "@tanstack/react-query";
import {Link as RouterLink, useNavigate} from "react-router";
import {receiptsGetAllOptions} from "@/api/@tanstack/react-query.gen";
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
import WarehousesSelect from "@/components/WarehousesSelect";
import ReceiptStatusChip from "@/components/receipts/ReceiptStatusChip";
import {
  RECEIPT_REASON_LABELS,
  RECEIPT_STATUS_LABELS,
  formatReceiptNumber,
} from "@/components/receipts/receiptUtils";
import type {ReceiptReason, ReceiptSortBy, ReceiptStatus} from "@/api/types.gen";

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

function ReceiptsPage() {
  const navigate = useNavigate();
  const canCreate = useHasPermission(["receipts.edit", "receipts.edit_assigned"]);

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

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "number", {
    defaultSortOrder: "desc",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      warehouseId: warehouseId ?? undefined,
      status: (status as ReceiptStatus) || undefined,
      reason: (reason as ReceiptReason) || undefined,
      sortBy,
      sortOrder,
    },
    [warehouseId, status, reason, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    receiptsGetAllOptions({query: fetchParams}),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Приемки", link: "/operations/receipts"}, {name: "Список"}]} />
      <PageGenericHeader
        title="Приемки"
        right={
          <>
            <IconButton color="inherit" onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
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
          onChange={(e) => setStatus(e.target.value as ReceiptStatus | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все статусы</MenuItem>
          {ALL_STATUSES.map((s) => (
            <MenuItem key={s} value={s}>
              {RECEIPT_STATUS_LABELS[s]}
            </MenuItem>
          ))}
        </Select>
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
              <TableCell>Причина</TableCell>
              <TableCell>Позиций</TableCell>
              <TableCell>Запланировано / Принято</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Приемки не найдены" />
            ) : (
              data?.items.map((receipt) => (
                <TableRow
                  key={receipt.id}
                  hover
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/operations/receipts/${receipt.id}`)}
                >
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
                      ? new Date(receipt.plannedDeliveryDate).toLocaleDateString("ru-RU")
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
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default ReceiptsPage;
