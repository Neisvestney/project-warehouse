import {useState, type ReactNode} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  IconButton,
  ListItemIcon,
  ListItemText,
  Menu,
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
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
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
import SelectionTableCell from "@/components/SelectionTableCell";
import BulkBar from "@/components/BulkBar";
import ConfirmDialog from "@/components/ConfirmDialog";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import NotesTableCell from "@/components/NotesTableCell";
import DateTimeTableCell from "@/components/DateTimeTableCell";
import WarehousesSelect from "@/components/WarehousesSelect";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import TagChips from "@/components/tags/TagChips";
import OrderStatusChip from "./OrderStatusChip";
import MarketplaceOrderFilters from "./marketplace/MarketplaceOrderFilters";
import {
  ALL_MARKETPLACE_ORDER_STATUSES,
  ALL_MARKETPLACE_TYPES,
} from "./marketplace/marketplaceOrderUtils";
import {ORDER_STATUS_LABELS, formatOrderNumber} from "./orderUtils";
import {getOrderBulkTransitions, type OrderBulkTransition} from "./orderBulkTransitions";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
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

/** Filtering by a status that stamps a timestamp adds that timestamp as a sortable column. */
const STATUS_DATE_COLUMNS: Partial<
  Record<
    OrderStatus,
    {key: OrderSortBy; label: string; get: (order: OrderSummaryDto) => string | null | undefined}
  >
> = {
  assembled: {key: "assembledAt", label: "Собран", get: (o) => o.assembledAt},
  shipped: {key: "shippedAt", label: "Отгружен", get: (o) => o.shippedAt},
};

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
  defaultPageSize?: number;
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
  defaultPageSize,
}: OrdersListPageProps) {
  const queryClient = useQueryClient();
  const canCreate = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const canSelfAssign = useHasPermission("orders.self_assign");

  const [failedItems, setFailedItems] = useState<BatchSelfAssignFailedItem[]>([]);
  const [selfAssignError, setSelfAssignError] = useState<string | null>(null);

  const [transitionFailed, setTransitionFailed] = useState<{
    verb: string;
    items: BatchTransitionStatusFailedItem[];
  } | null>(null);
  const [transitionError, setTransitionError] = useState<string | null>(null);
  const [activeTransition, setActiveTransition] = useState<OrderBulkTransition | null>(null);
  const [confirmTransition, setConfirmTransition] = useState<OrderBulkTransition | null>(null);
  const [moreAnchor, setMoreAnchor] = useState<HTMLElement | null>(null);

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

  const [catalogItemId, setCatalogItemId] = useSyncedWithQueryState(
    "item",
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

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const statusDateColumn = status ? STATUS_DATE_COLUMNS[status] : undefined;
  const sortColumns = statusDateColumn ? [...SORT_COLUMNS, statusDateColumn] : SORT_COLUMNS;

  const {sortBy, sortOrder, handleSortClick} = useTableSort(sortColumns, "number", {
    defaultSortOrder: "desc",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      type,
      warehouseId: warehouseId ?? undefined,
      status: (status as OrderStatus) || undefined,
      catalogItemId: catalogItemId ?? undefined,
      tagIds: tagIds.length > 0 ? tagIds : undefined,
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
      catalogItemId,
      tagIds,
      marketplaceType,
      marketplaceAccountId,
      marketplaceStatus,
      sortBy,
      sortOrder,
    ],
    {
      defaultPageSize: defaultPageSize ?? 20,
    },
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

  const transitionMutation = useOperationMutation(
    "order.transition_status",
    {
      ...ordersBatchTransitionStatusMutation(),
      meta: {suppressGlobalError: true},
      // Awaited so isPending covers the refetch and the button cannot re-send stale ids
      onSuccess: async (data) => {
        removeIds(data.transitionedOrderIds);
        await queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      },
      onError: (error) => setTransitionError(extractErrorMessage(error)),
    },
    (variables) => ({
      "order.count": variables.body?.orderIds.length ?? 0,
      "order.target_status": variables.body?.targetStatus ?? "",
    }),
  );

  const selectedConfirmedIds = selectedItems
    .filter((o) => o.status === "confirmed")
    .map((o) => o.id);

  const idsFor = (transition: OrderBulkTransition) =>
    selectedItems.filter((o) => transition.from.includes(o.status)).map((o) => o.id);

  // canCreate is really "can edit orders" (orders.edit / orders.edit_assigned) — status transitions need the same permission
  const availableTransitions = canCreate
    ? getOrderBulkTransitions(type)
        .map((transition) => ({transition, count: idsFor(transition).length}))
        .filter(({count}) => count > 0)
    : [];
  const primaryTransitions = availableTransitions.filter(({transition}) => transition.primary);
  const menuTransitions = availableTransitions.filter(({transition}) => !transition.primary);

  const showSelfAssign = canSelfAssign && selectedConfirmedIds.length > 0;
  const showBulkBar =
    selectedItems.length > 0 &&
    (showSelfAssign || availableTransitions.length > 0 || bulkActions != null);
  const columnCount =
    (showNotes ? 9 : 8) + (extraColumns?.length ?? 0) + (statusDateColumn ? 1 : 0);

  function handleSelfAssignSelected() {
    setFailedItems([]);
    setSelfAssignError(null);
    selfAssignMutation.mutate({body: {orderIds: selectedConfirmedIds}});
  }

  function runTransition(transition: OrderBulkTransition) {
    const orderIds = idsFor(transition);
    if (orderIds.length === 0) return;
    setTransitionFailed(null);
    setTransitionError(null);
    setActiveTransition(transition);
    transitionMutation.mutate(
      {body: {orderIds, targetStatus: transition.to}},
      {
        onSuccess: (data) =>
          setTransitionFailed(
            data.failedItems.length > 0
              ? {verb: transition.failedVerb, items: data.failedItems}
              : null,
          ),
        onSettled: () => {
          setConfirmTransition(null);
          setActiveTransition(null);
        },
      },
    );
  }

  function handleTransitionClick(transition: OrderBulkTransition) {
    setMoreAnchor(null);
    if (transition.confirm) setConfirmTransition(transition);
    else runTransition(transition);
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
        <CatalogItemsSelect
          value={catalogItemId}
          onChange={setCatalogItemId}
          sx={{flexBasis: 300}}
          size="small"
          textFieldProps={{label: "Содержит позицию"}}
        />
        <DocumentTagsFilter
          kind="order"
          value={tagIds}
          onChange={setTagIds}
          sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
        />
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
          {primaryTransitions.map(({transition, count}) => (
            <Button
              key={transition.key}
              size="small"
              variant="contained"
              color="inherit"
              startIcon={
                transitionMutation.isPending && activeTransition?.key === transition.key ? (
                  <CircularProgress size={14} color="inherit" />
                ) : (
                  transition.icon
                )
              }
              disabled={transitionMutation.isPending}
              onClick={() => handleTransitionClick(transition)}
              sx={{color: "primary.main"}}
            >
              {transition.label} ({count})
            </Button>
          ))}
          {menuTransitions.length > 0 && (
            <>
              <Button
                size="small"
                variant="contained"
                color="inherit"
                endIcon={
                  transitionMutation.isPending && activeTransition && !activeTransition.primary ? (
                    <CircularProgress size={14} color="inherit" />
                  ) : (
                    <ArrowDropDownIcon />
                  )
                }
                disabled={transitionMutation.isPending}
                onClick={(e) => setMoreAnchor(e.currentTarget)}
                sx={{color: "primary.main"}}
              >
                Ещё
              </Button>
              <Menu
                anchorEl={moreAnchor}
                open={moreAnchor != null}
                onClose={() => setMoreAnchor(null)}
              >
                {menuTransitions.map(({transition, count}) => (
                  <MenuItem
                    key={transition.key}
                    onClick={() => handleTransitionClick(transition)}
                    sx={
                      transition.danger
                        ? {color: "error.main", "& .MuiListItemIcon-root": {color: "inherit"}}
                        : undefined
                    }
                  >
                    <ListItemIcon>{transition.icon}</ListItemIcon>
                    <ListItemText>
                      {transition.label} ({count})
                    </ListItemText>
                  </MenuItem>
                ))}
              </Menu>
            </>
          )}
        </BulkBar>
      )}

      {confirmTransition?.confirm && (
        <ConfirmDialog
          open
          onClose={() => setConfirmTransition(null)}
          title={confirmTransition.confirm.title}
          onConfirm={() => runTransition(confirmTransition)}
          isPending={transitionMutation.isPending}
          confirmText={confirmTransition.confirm.confirmText}
          confirmColor={confirmTransition.danger ? "error" : "primary"}
        >
          <Typography variant="body2" sx={{mb: 1}}>
            Будет затронуто: {pluralCount(idsFor(confirmTransition).length, NOUNS.order)}.
          </Typography>
          <Typography variant="body2">{confirmTransition.confirm.text}</Typography>
        </ConfirmDialog>
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

      {transitionFailed && (
        <Alert severity="error" onClose={() => setTransitionFailed(null)}>
          <Typography variant="body2" sx={{mb: 0.5}}>
            Часть заказов не удалось {transitionFailed.verb}:
          </Typography>
          {transitionFailed.items.map((f) => (
            <Typography key={f.orderId} variant="caption" sx={{display: "block"}}>
              • {f.orderNumber != null ? formatOrderNumber(f.orderNumber) : "Заказ"}:{" "}
              {resolveErrorMessage(f.error)}
            </Typography>
          ))}
        </Alert>
      )}

      {transitionError && (
        <Alert severity="error" onClose={() => setTransitionError(null)}>
          {transitionError}
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
              <SelectionTableCell
                checked={allPageSelected}
                indeterminate={!allPageSelected && somePageSelected}
                onCheck={() => toggleAll()}
              />
              {sortColumns.map(({key, label}) => (
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
              <TableCell>Позиций</TableCell>
              <TableCell>Теги</TableCell>
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
                  <SelectionTableCell
                    checked={isSelected(order.id)}
                    onCheck={(extendRange) => toggle(order, extendRange)}
                  />
                  <TableCell sx={{fontFamily: "monospace"}}>
                    {formatOrderNumber(order.number)}
                  </TableCell>
                  <TableCell>
                    <OrderStatusChip status={order.status} />
                  </TableCell>
                  <TableCell>{order.warehouseName}</TableCell>
                  <DateTimeTableCell value={order.plannedShipmentAt} />
                  <DateTimeTableCell value={order.createdAt} />
                  {statusDateColumn && <DateTimeTableCell value={statusDateColumn.get(order)} />}
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
                      <Chip variant={"outlined"} size={"small"} label={order.componentCount} />
                    )}
                  </TableCell>
                  <TableCell>
                    <TagChips tags={order.tags} />
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
