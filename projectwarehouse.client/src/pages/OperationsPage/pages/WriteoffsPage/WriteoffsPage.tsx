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
import {writeoffsGetAllOptions} from "@/api/@tanstack/react-query.gen";
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
import WriteoffStatusChip from "@/components/writeoffs/WriteoffStatusChip";
import {
  WRITEOFF_REASON_LABELS,
  WRITEOFF_STATUS_LABELS,
  formatWriteoffNumber,
} from "@/components/writeoffs/writeoffUtils";
import type {WriteoffReason, WriteoffSortBy, WriteoffStatus} from "@/api/types.gen";

const SORT_COLUMNS: {key: WriteoffSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "name", label: "Название"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "createdAt", label: "Создано"},
];

const ALL_STATUSES: WriteoffStatus[] = ["draft", "finished", "canceled"];
const ALL_REASONS: WriteoffReason[] = ["loss", "defect", "other"];

function WriteoffsPage() {
  const navigate = useNavigate();
  const canCreate = useHasPermission(["writeoffs.edit", "writeoffs.edit_assigned"]);

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

  const [status, setStatus] = useSyncedWithQueryState<WriteoffStatus | "">(
    "status",
    (q) => (ALL_STATUSES.includes(q as WriteoffStatus) ? (q as WriteoffStatus) : ""),
    (v) => v || null,
  );

  const [reason, setReason] = useSyncedWithQueryState<WriteoffReason | "">(
    "reason",
    (q) => (ALL_REASONS.includes(q as WriteoffReason) ? (q as WriteoffReason) : ""),
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
      status: (status as WriteoffStatus) || undefined,
      reason: (reason as WriteoffReason) || undefined,
      sortBy,
      sortOrder,
    },
    [warehouseId, status, reason, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    writeoffsGetAllOptions({query: fetchParams}),
  );

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
          onChange={(e) => setStatus(e.target.value as WriteoffStatus | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все статусы</MenuItem>
          {ALL_STATUSES.map((s) => (
            <MenuItem key={s} value={s}>
              {WRITEOFF_STATUS_LABELS[s]}
            </MenuItem>
          ))}
        </Select>
        <Select
          value={reason}
          onChange={(e) => setReason(e.target.value as WriteoffReason | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все причины</MenuItem>
          {ALL_REASONS.map((r) => (
            <MenuItem key={r} value={r}>
              {WRITEOFF_REASON_LABELS[r]}
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
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={7} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={7} message="Списания не найдены" />
            ) : (
              data?.items.map((writeoff) => (
                <TableRow
                  key={writeoff.id}
                  hover
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/operations/writeoffs/${writeoff.id}`)}
                >
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
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default WriteoffsPage;
