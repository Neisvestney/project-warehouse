import {useState, type ReactNode} from "react";
import {
  Alert,
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
  type TableCellProps,
  TableHead,
  TableRow,
  TableSortLabel,
  Typography,
  Chip,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {Link as RouterLink} from "react-router";
import {
  ordersBatchSelfAssignMutation,
  ordersBatchTransitionStatusMutation,
  ordersGetAllOptions,
  ordersGetAllQueryKey,
} from "@/api/@tanstack/react-query.gen";
import {useDebouncedSyncedWithQueryState} from "@/hooks/useDebouncedSyncedWithQueryState";
import {usePaginatedParams} from "@/hooks/usePaginatedParams";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useTableSort} from "@/hooks/useTableSort";
import {useSelectedItems} from "@/hooks/useSelectedItems";
import {useHasPermission} from "@/hooks/usePermission";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import SearchInput from "@/components/SearchInput";
import FiltersBar from "@/components/FiltersBar";
import DataTableContainer from "@/components/DataTableContainer";
import BulkBar from "@/components/BulkBar";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import NotesTableCell from "@/components/NotesTableCell";
import WarehousesSelect from "@/components/WarehousesSelect";
import OrderStatusChip from "./OrderStatusChip";
import MarketplaceOrderFilters from "./marketplace/MarketplaceOrderFilters";
import {
  ALL_MARKETPLACE_ORDER_STATUSES,
  ALL_MARKETPLACE_TYPES,
} from "./marketplace/marketplaceOrderUtils";
import {ORDER_STATUS_LABELS, formatOrderNumber} from "./orderUtils";
import type {
  BatchSelfAssignFailedItem,
  BatchTransitionStatusFailedItem,
  MarketplaceOrderStatus,
  MarketplaceType,
  OrderSortBy,
  OrderStatus,
  OrderSummaryDto,
  OrderType,
} from "@/api/types.gen";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";

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

export interface OrdersListExtraColumn {
  key: string;
  label: string;
  render: (order: OrderSummaryDto) => ReactNode;
  align?: TableCellProps["align"];
}

interface OrdersListPageProps {
  type: OrderType;
  title: string;
  breadcrumbName: string;
  breadcrumbLink: string;
  createLink?: string;
  /** Rendered in the page header. Keeps marketplace specifics out of this shared component. */
  headerActions?: ReactNode;
  /** Extra buttons for the selection toolbar. Receives every selected order, not just confirmed ones. */
  bulkActions?: (selectedOrders: OrderSummaryDto[]) => ReactNode;
  extraColumns?: OrdersListExtraColumn[];
  /** Marketplace / account / posting-status filters. Meaningless on Direct orders. */
  marketplaceFilters?: boolean;
  /** FBO trades the notes column for the posting number. */
  showNotes?: boolean;
}

const getOrderId = (order: OrderSummaryDto) => order.id;

function OrdersListPage({
  type,
  title,
  breadcrumbName,
  breadcrumbLink,
  createLink,
  headerActions,
  bulkActions,
  extraColumns,
  marketplaceFilters,
  showNotes = true,
}: OrdersListPageProps) {
  const queryClient = useQueryClient();
  const canCreate = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const canSelfAssign = useHasPermission("orders.self_assign");

  const [failedItems, setFailedItems] = useState<BatchSelfAssignFailedItem[]>([]);
  const [selfAssignError, setSelfAssignError] = useState<string | null>(null);

  const [shipFailedItems, setShipFailedItems] = useState<BatchTransitionStatusFailedItem[]>([]);
  const [shipError, setShipError] = useState<string | null>(null);

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

  const [marketplaceType, setMarketplaceType] = useSyncedWithQueryState<MarketplaceType | "">(
    "marketplace",
    (q) => (ALL_MARKETPLACE_TYPES.includes(q as MarketplaceType) ? (q as MarketplaceType) : ""),
    (v) => v || null,
  );

  const [marketplaceAccountId, setMarketplaceAccountId] = useSyncedWithQueryState(
    "account",
    (q) => (typeof q === "string" ? q : null),
    (v) => v,
  );

  const [marketplaceStatus, setMarketplaceStatus] = useSyncedWithQueryState<
    MarketplaceOrderStatus | ""
  >(
    "mpStatus",
    (q) =>
      ALL_MARKETPLACE_ORDER_STATUSES.includes(q as MarketplaceOrderStatus)
        ? (q as MarketplaceOrderStatus)
        : "",
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
      marketplaceType: marketplaceFilters
        ? (marketplaceType as MarketplaceType) || undefined
        : undefined,
      marketplaceAccountId: marketplaceFilters ? (marketplaceAccountId ?? undefined) : undefined,
      marketplaceStatus: marketplaceFilters
        ? (marketplaceStatus as MarketplaceOrderStatus) || undefined
        : undefined,
      sortBy,
      sortOrder,
    },
    [
      warehouseId,
      status,
      marketplaceType,
      marketplaceAccountId,
      marketplaceStatus,
      sortBy,
      sortOrder,
    ],
  );

  // the account list is scoped to the marketplace, so a stale id must not survive the switch
  function handleMarketplaceTypeChange(value: MarketplaceType | "") {
    setMarketplaceType(value);
    setMarketplaceAccountId(null);
  }

  const {data, isLoading, isFetching, refetch} = useQuery(
    ordersGetAllOptions({query: fetchParams}),
  );

  const {
    selectedItems,
    isSelected,
    allPageSelected,
    somePageSelected,
    toggle,
    toggleAll,
    removeIds,
    clear,
  } = useSelectedItems(getOrderId, data?.items);

  const selfAssignMutation = useOperationMutation(
    "order.self_assign",
    {
      ...ordersBatchSelfAssignMutation(),
      meta: {suppressGlobalError: true},
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: async (data) => {
        removeIds(data.assignedOrderIds);
        setFailedItems(data.failedItems);
        await queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      },
      onError: (error) => setSelfAssignError(extractErrorMessage(error)),
    },
    (variables) => ({"order.count": variables.body?.orderIds.length ?? 0}),
  );

  const shipMutation = useOperationMutation(
    "order.ship",
    {
      ...ordersBatchTransitionStatusMutation(),
      meta: {suppressGlobalError: true},
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: async (data) => {
        removeIds(data.transitionedOrderIds);
        setShipFailedItems(data.failedItems);
        await queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      },
      onError: (error) => setShipError(extractErrorMessage(error)),
    },
    (variables) => ({"order.count": variables.body?.orderIds.length ?? 0}),
  );

  const selectedConfirmedIds = selectedItems
    .filter((o) => o.status === "confirmed")
    .map((o) => o.id);

  const selectedAssembledIds = selectedItems
    .filter((o) => o.status === "assembled")
    .map((o) => o.id);

  const showSelfAssign = canSelfAssign && selectedConfirmedIds.length > 0;
  // canCreate is really "can edit orders" (orders.edit / orders.edit_assigned) — shipping needs the same permission
  const showShip = canCreate && selectedAssembledIds.length > 0;
  // the bulkActions term keeps the bar hidden on Direct and FBO, which pass no extra actions
  const showBulkBar =
    selectedItems.length > 0 && (showSelfAssign || showShip || bulkActions != null);
  const columnCount = (showNotes ? 8 : 7) + (extraColumns?.length ?? 0);

  function handleSelfAssignSelected() {
    setFailedItems([]);
    setSelfAssignError(null);
    selfAssignMutation.mutate({body: {orderIds: selectedConfirmedIds}});
  }

  function handleShipSelected() {
    setShipFailedItems([]);
    setShipError(null);
    shipMutation.mutate({body: {orderIds: selectedAssembledIds, targetStatus: "shipped"}});
  }

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Заказы", link: breadcrumbLink}, {name: breadcrumbName}]} />
      <PageGenericHeader
        title={title}
        refresh={
          <IconButton color="inherit" onClick={() => refetch()}>
            <RefreshIcon />
          </IconButton>
        }
        actions={
          <>
            {headerActions}
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
        {marketplaceFilters && (
          <MarketplaceOrderFilters
            type={marketplaceType}
            onTypeChange={handleMarketplaceTypeChange}
            accountId={marketplaceAccountId}
            onAccountChange={setMarketplaceAccountId}
            status={marketplaceStatus}
            onStatusChange={setMarketplaceStatus}
          />
        )}
      </FiltersBar>

      {type == "fbo" && <Alert severity={"warning"}>Раздел "Поставки FBO" еще не реализован</Alert>}

      {showBulkBar && (
        <BulkBar
          count={selectedItems.length}
          countLabel={{one: "заказ выбран", few: "заказа выбрано", many: "заказов выбрано"}}
          onClear={clear}
        >
          {bulkActions?.(selectedItems)}
          {showSelfAssign && (
            <Button
              size="small"
              variant="contained"
              color="inherit"
              startIcon={
                selfAssignMutation.isPending ? (
                  <CircularProgress size={14} color="inherit" />
                ) : (
                  <AssignmentIndIcon />
                )
              }
              disabled={selfAssignMutation.isPending}
              onClick={handleSelfAssignSelected}
              sx={{color: "primary.main"}}
            >
              Взять на себя ({selectedConfirmedIds.length})
            </Button>
          )}
          {showShip && (
            <Button
              size="small"
              variant="contained"
              color="inherit"
              startIcon={
                shipMutation.isPending ? (
                  <CircularProgress size={14} color="inherit" />
                ) : (
                  <LocalShippingIcon />
                )
              }
              disabled={shipMutation.isPending}
              onClick={handleShipSelected}
              sx={{color: "primary.main"}}
            >
              Отгрузить ({selectedAssembledIds.length})
            </Button>
          )}
        </BulkBar>
      )}

      {failedItems.length > 0 && (
        <Alert severity="error" onClose={() => setFailedItems([])}>
          <Typography variant="body2" sx={{mb: 0.5}}>
            Часть заказов не удалось взять на себя:
          </Typography>
          {failedItems.map((f) => (
            <Typography key={f.orderId} variant="caption" sx={{display: "block"}}>
              • {f.orderNumber != null ? formatOrderNumber(f.orderNumber) : "Заказ"}:{" "}
              {resolveErrorMessage(f.error)}
            </Typography>
          ))}
        </Alert>
      )}

      {selfAssignError && (
        <Alert severity="error" onClose={() => setSelfAssignError(null)}>
          {selfAssignError}
        </Alert>
      )}

      {shipFailedItems.length > 0 && (
        <Alert severity="error" onClose={() => setShipFailedItems([])}>
          <Typography variant="body2" sx={{mb: 0.5}}>
            Часть заказов не удалось отгрузить:
          </Typography>
          {shipFailedItems.map((f) => (
            <Typography key={f.orderId} variant="caption" sx={{display: "block"}}>
              • {f.orderNumber != null ? formatOrderNumber(f.orderNumber) : "Заказ"}:{" "}
              {resolveErrorMessage(f.error)}
            </Typography>
          ))}
        </Alert>
      )}

      {shipError && (
        <Alert severity="error" onClose={() => setShipError(null)}>
          {shipError}
        </Alert>
      )}

      <DataTableContainer
        isFetching={isFetching}
        count={data?.total ?? 0}
        page={page}
        onPageChange={setPage}
        rowsPerPage={pageSize}
        onRowsPerPageChange={setPageSize}
        rowsPerPageOptions={[10, 20, 50, 100, 200]}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell padding="checkbox">
                <Checkbox
                  size="small"
                  checked={allPageSelected}
                  indeterminate={!allPageSelected && somePageSelected}
                  onChange={toggleAll}
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
              {extraColumns?.map(({key, label, align}) => (
                <TableCell key={key} align={align}>
                  {label}
                </TableCell>
              ))}
              {showNotes && <TableCell>Заметки</TableCell>}
              <TableCell>Коробок</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={columnCount} />
            ) : data?.items.length === 0 ? (
              <TableRowEmpty colSpan={columnCount} message="Заказы не найдены" />
            ) : (
              data?.items.map((order) => (
                <LinkTableRow
                  key={order.id}
                  to={`/operations/orders/${order.id}`}
                  ariaLabel={`Заказ ${formatOrderNumber(order.number)}`}
                  selected={isSelected(order.id)}
                  sx={{
                    opacity: isFetching && !isLoading ? 0.5 : 1,
                    transition: "opacity 0.2s",
                  }}
                >
                  <TableCell padding="checkbox">
                    <Checkbox
                      size="small"
                      checked={isSelected(order.id)}
                      onClick={() => toggle(order)}
                      sx={{position: "relative", zIndex: 1}}
                    />
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
                  {extraColumns?.map(({key, render, align}) => (
                    <TableCell key={key} align={align}>
                      {render(order)}
                    </TableCell>
                  ))}
                  {showNotes && (
                    <NotesTableCell notes={order.notes} sx={{position: "relative", zIndex: 1}} />
                  )}
                  <TableCell>
                    {!order.boxCount ? (
                      "—"
                    ) : (
                      <Chip variant={"outlined"} size={"small"} label={order.boxCount} />
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

export default OrdersListPage;
