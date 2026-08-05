import {useState} from "react";
import {
  Button,
  Checkbox,
  CircularProgress,
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
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import {useQuery, useMutation, useQueryClient} from "@tanstack/react-query";
import {Link as RouterLink, useNavigate} from "react-router";
import {
  ordersGetAllOptions,
  ordersGetAllQueryKey,
  ordersSelfAssignMutation,
} from "@/api/@tanstack/react-query.gen";
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
import OrderStatusChip from "./OrderStatusChip";
import {ORDER_STATUS_LABELS, formatOrderNumber} from "./orderUtils";
import type {OrderSortBy, OrderStatus, OrderType} from "@/api/types.gen";
import {pluralCount} from "@/utils/pluralUtils";

const SORT_COLUMNS: {key: OrderSortBy; label: string}[] = [
  {key: "number", label: "#"},
  {key: "status", label: "Статус"},
  {key: "warehouseName", label: "Склад"},
  {key: "plannedShipmentAt", label: "Плановая отгрузка"},
  {key: "createdAt", label: "Создан"},
];

const ALL_STATUSES: OrderStatus[] = [
  "draft",
  "confirmed",
  "assembly",
  "assembled",
  "shipped",
  "canceled",
];

interface OrdersListPageProps {
  type: OrderType;
  title: string;
  breadcrumbName: string;
  breadcrumbLink: string;
  createLink?: string;
}

function OrdersListPage({
  type,
  title,
  breadcrumbName,
  breadcrumbLink,
  createLink,
}: OrdersListPageProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canCreate = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const canSelfAssign = useHasPermission("orders.self_assign");

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [selfAssigning, setSelfAssigning] = useState(false);

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

  const [status, setStatus] = useSyncedWithQueryState<OrderStatus | "">(
    "status",
    (q) => (ALL_STATUSES.includes(q as OrderStatus) ? (q as OrderStatus) : ""),
    (v) => v || null,
  );

  const {sortBy, sortOrder, handleSortClick} = useTableSort(SORT_COLUMNS, "number", {
    defaultSortOrder: "desc",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      type,
      warehouseId: warehouseId ?? undefined,
      status: (status as OrderStatus) || undefined,
      sortBy,
      sortOrder,
    },
    [warehouseId, status, sortBy, sortOrder],
  );

  const {data, isLoading, isFetching, refetch} = useQuery(
    ordersGetAllOptions({query: fetchParams}),
  );

  const selfAssignMutation = useMutation({
    ...ordersSelfAssignMutation(),
    meta: {suppressGlobalError: true},
  });

  const selectedConfirmedIds =
    data?.items.filter((o) => selectedIds.has(o.id) && o.status === "confirmed").map((o) => o.id) ??
    [];

  const showBulkBar = canSelfAssign && selectedConfirmedIds.length > 0;

  async function handleSelfAssignSelected() {
    setSelfAssigning(true);
    for (const id of selectedConfirmedIds) {
      try {
        await selfAssignMutation.mutateAsync({path: {id}});
      } catch {
        // continue with others on failure
      }
    }
    setSelectedIds(new Set());
    queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
    setSelfAssigning(false);
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleSelectAll() {
    const pageIds = data?.items.map((o) => o.id) ?? [];
    const allSelected = pageIds.every((id) => selectedIds.has(id));
    if (allSelected) {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        pageIds.forEach((id) => next.delete(id));
        return next;
      });
    } else {
      setSelectedIds((prev) => new Set([...prev, ...pageIds]));
    }
  }

  const pageIds = data?.items.map((o) => o.id) ?? [];
  const allPageSelected = pageIds.length > 0 && pageIds.every((id) => selectedIds.has(id));
  const somePageSelected = pageIds.some((id) => selectedIds.has(id));

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Заказы", link: breadcrumbLink}, {name: breadcrumbName}]} />
      <PageGenericHeader
        title={title}
        right={
          <>
            <IconButton color="inherit" onClick={() => refetch()}>
              <RefreshIcon />
            </IconButton>
            {createLink && canCreate && (
              <Button
                variant="outlined"
                endIcon={<AddIcon />}
                size="small"
                component={RouterLink}
                to={createLink}
              >
                Новый заказ
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
          onChange={(e) => setStatus(e.target.value as OrderStatus | "")}
          size="small"
          displayEmpty
          sx={{minWidth: 160}}
        >
          <MenuItem value="">Все статусы</MenuItem>
          {ALL_STATUSES.map((s) => (
            <MenuItem key={s} value={s}>
              {ORDER_STATUS_LABELS[s]}
            </MenuItem>
          ))}
        </Select>
      </FiltersBar>

      {showBulkBar && (
        <Toolbar
          variant="dense"
          sx={{
            bgcolor: "primary.main",
            color: "primary.contrastText",
            borderRadius: 1,
            gap: 1,
          }}
        >
          <Typography variant="body2" sx={{flexGrow: 1}}>
            {pluralCount(selectedConfirmedIds.length, {
              one: "подтверждённый заказ выбран",
              few: "подтверждённых заказа выбрано",
              many: "подтверждённых заказов выбрано",
            })}
          </Typography>
          <Button
            size="small"
            variant="contained"
            color="inherit"
            startIcon={
              selfAssigning ? <CircularProgress size={14} color="inherit" /> : <AssignmentIndIcon />
            }
            disabled={selfAssigning}
            onClick={handleSelfAssignSelected}
            sx={{color: "primary.main", bgcolor: "white"}}
          >
            Взять на себя
          </Button>
        </Toolbar>
      )}

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
              <TableCell padding="checkbox">
                <Checkbox
                  size="small"
                  checked={allPageSelected}
                  indeterminate={!allPageSelected && somePageSelected}
                  onChange={toggleSelectAll}
                />
              </TableCell>
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
              <TableCell>Заметки</TableCell>
              <TableCell>Коробок</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={8} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={8} message="Заказы не найдены" />
            ) : (
              data?.items.map((order) => (
                <TableRow
                  key={order.id}
                  hover
                  selected={selectedIds.has(order.id)}
                  sx={{
                    cursor: "pointer",
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                  onClick={() => navigate(`/operations/orders/${order.id}`)}
                >
                  <TableCell
                    padding="checkbox"
                    onClick={(e) => {
                      e.stopPropagation();
                      toggleSelect(order.id);
                    }}
                  >
                    <Checkbox size="small" checked={selectedIds.has(order.id)} />
                  </TableCell>
                  <TableCell sx={{fontFamily: "monospace"}}>
                    {formatOrderNumber(order.number)}
                  </TableCell>
                  <TableCell>
                    <OrderStatusChip status={order.status} />
                  </TableCell>
                  <TableCell>{order.warehouseName}</TableCell>
                  <TableCell>
                    {order.plannedShipmentAt
                      ? new Date(order.plannedShipmentAt).toLocaleDateString("ru-RU")
                      : "—"}
                  </TableCell>
                  <TableCell>{new Date(order.createdAt).toLocaleDateString("ru-RU")}</TableCell>
                  <TableCell>
                    <Tooltip title={order.notes ?? ""} disableHoverListener={!order.notes}>
                      <Typography
                        variant="body2"
                        noWrap
                        sx={{maxWidth: 200, display: "inline-block"}}
                      >
                        {order.notes ?? "—"}
                      </Typography>
                    </Tooltip>
                  </TableCell>
                  <TableCell>{order.boxCount}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default OrdersListPage;
