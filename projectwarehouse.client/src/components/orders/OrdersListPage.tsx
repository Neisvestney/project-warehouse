import {useState, type ReactNode} from "react";
import {
  Alert,
  Button,
  Chip,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  type TableCellProps,
  TableHead,
  TableRow,
  TableSortLabel,
  ToggleButton,
  Tooltip,
  Typography,
} from "@mui/material";
import AltRouteIcon from "@mui/icons-material/AltRoute";
import AddIcon from "@mui/icons-material/Add";
import RefreshIcon from "@mui/icons-material/Refresh";
import AssignmentIndIcon from "@mui/icons-material/AssignmentInd";
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
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {useOperationMutation} from "@/hooks/useOperationMutation";
import PageGenericHeader from "@/components/PageGenericHeader";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import FiltersBar from "@/components/FiltersBar";
import DataTableContainer from "@/components/DataTableContainer";
import SelectionTableCell from "@/components/SelectionTableCell";
import BulkBar, {type BulkAction} from "@/components/BulkBar";
import type {TableInfoStat} from "@/components/TableInfoBar";
import ConfirmDialog from "@/components/ConfirmDialog";
import TableRowLoader from "@/components/TableRowLoader";
import TableRowEmpty from "@/components/TableRowEmpty";
import LinkTableRow from "@/components/LinkTableRow";
import NotesTableCell from "@/components/NotesTableCell";
import DateTimeTableCell from "@/components/DateTimeTableCell";
import WarehousesSelect from "@/components/WarehousesSelect";
import SearchWithItemsInput from "@/components/catalog/SearchWithItemsInput";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import TagChips from "@/components/tags/TagChips";
import OrderStatusChip from "./OrderStatusChip";
import OrderStatusTabs from "./OrderStatusTabs";
import OrderCompositionPreview from "./OrderCompositionPreview";
import MarketplaceOrderFilters from "./marketplace/MarketplaceOrderFilters";
import {
  ALL_MARKETPLACE_ORDER_STATUSES,
  ALL_MARKETPLACE_TYPES,
} from "./marketplace/marketplaceOrderUtils";
import {formatOrderNumber} from "./orderUtils";
import {getOrderBulkTransitions, type OrderBulkTransition} from "./orderBulkTransitions";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import type {
  BatchSelfAssignFailedItem,
  BatchTransitionStatusFailedItem,
  MarketplaceOrderStatus,
  MarketplaceType,
  OrderOverdueKind,
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

const ALL_OVERDUE_KINDS: OrderOverdueKind[] = ["assembly", "shipment"];

export interface OrdersListExtraColumn {
  key: string;
  label: string;
  render: (order: OrderSummaryDto) => ReactNode;
  align?: TableCellProps["align"];
  /** Keeps the header and the cells on one line. */
  noWrap?: boolean;
}

interface OrdersListPageProps {
  type: OrderType;
  title: string;
  breadcrumbName: string;
  breadcrumbLink: string;
  createLink?: string;
  /** Rendered in the page header. Keeps marketplace specifics out of this shared component. */
  headerActions?: ReactNode;
  /** Extra selection toolbar actions. Receives every selected order, not just confirmed ones. */
  bulkActions?: (selectedOrders: OrderSummaryDto[]) => BulkAction[];
  extraColumns?: OrdersListExtraColumn[];
  /** Marketplace / account / posting-status filters. Meaningless on Direct orders. */
  marketplaceFilters?: boolean;
  /**
   * Стартовое положение тумблера «Внешние». Включено там, где внешними являются все заказы раздела —
   * иначе список открывался бы пустым.
   */
  defaultIncludeExternal?: boolean;
  /** Показывать тумблер «Внешние». */
  showExternalFilter?: boolean;
  /** Колонки, ненужные разделу, вместе со своими фильтрами в панели. */
  hiddenColumns?: OrderSortBy[];
  /** Статусы, чьи даты показываются всегда, а не только при фильтре по этому статусу. */
  alwaysShownStatusDates?: OrderStatus[];
  /** Заголовки статусных дат, если в разделе отметка означает не то же, что в складском заказе. */
  statusDateLabels?: Partial<Record<OrderStatus, string>>;
  /** FBO trades the notes column for the posting number. */
  showNotes?: boolean;
  defaultPageSize?: number;
}

const EMPTY_HIDDEN_COLUMNS: OrderSortBy[] = [];
const EMPTY_STATUS_DATES: OrderStatus[] = [];
const EMPTY_STATUS_DATE_LABELS: Partial<Record<OrderStatus, string>> = {};

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
  defaultIncludeExternal = false,
  showExternalFilter = true,
  hiddenColumns = EMPTY_HIDDEN_COLUMNS,
  alwaysShownStatusDates = EMPTY_STATUS_DATES,
  statusDateLabels = EMPTY_STATUS_DATE_LABELS,
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

  const [catalogItemIds, setCatalogItemIds] = useSyncedWithQueryState<string[]>(
    "item",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
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

  // внешние заказы разбавляют рабочий список тем, чего на складе никогда не было
  const [includeExternal, setIncludeExternal] = useSyncedWithQueryState<boolean>(
    "external",
    (q) => (q === null ? defaultIncludeExternal : q === "1"),
    (v) => (v === defaultIncludeExternal ? null : v ? "1" : "0"),
  );

  const [overdue, setOverdue] = useSyncedWithQueryState<OrderOverdueKind | "">(
    "overdue",
    (q) => (ALL_OVERDUE_KINDS.includes(q as OrderOverdueKind) ? (q as OrderOverdueKind) : ""),
    (v) => v || null,
  );

  const [tagIds, setTagIds] = useSyncedWithQueryState<string[]>(
    "tags",
    (q) => (typeof q === "string" && q ? q.split(",").filter(Boolean) : []),
    (v) => v.join(",") || null,
  );

  const isHidden = (key: OrderSortBy) => hiddenColumns.includes(key);
  const showStatusFilter = !isHidden("status");
  const showWarehouseFilter = !isHidden("warehouseName");
  const statusFilter = showStatusFilter ? status : "";

  const statusDateStatuses = [
    ...alwaysShownStatusDates,
    ...(statusFilter && !alwaysShownStatusDates.includes(statusFilter) ? [statusFilter] : []),
  ];
  const statusDateColumns = statusDateStatuses
    .map((s) => {
      const column = STATUS_DATE_COLUMNS[s];
      const label = statusDateLabels[s];
      return column && label ? {...column, label} : column;
    })
    .filter((c) => c !== undefined);
  const sortColumns = [...SORT_COLUMNS.filter((c) => !isHidden(c.key)), ...statusDateColumns];

  const {sortBy, sortOrder, handleSortClick} = useTableSort(sortColumns, "number", {
    defaultSortOrder: "desc",
  });

  const {fetchParams, page, setPage, pageSize, setPageSize} = usePaginatedParams(
    {searchString: searchString || undefined},
    [searchString],
    {
      type,
      warehouseId: (showWarehouseFilter ? warehouseId : null) ?? undefined,
      status: statusFilter || undefined,
      catalogItemIds: catalogItemIds.length > 0 ? catalogItemIds : undefined,
      tagIds: tagIds.length > 0 ? tagIds : undefined,
      overdue: overdue || undefined,
      includeExternal: includeExternal || undefined,
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
      catalogItemIds,
      tagIds,
      overdue,
      includeExternal,
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

  function handleMarketplaceSourceChange(type: MarketplaceType | "", accountId: string | null) {
    setMarketplaceType(type);
    setMarketplaceAccountId(accountId);
  }

  const {data, isLoading, isFetching, refetch} = useQuery(
    ordersGetAllOptions({query: fetchParams}),
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

  const listStats: TableInfoStat[] = [
    {key: "total", label: "Всего заказов:", value: (data?.total ?? 0).toLocaleString("ru-RU")},
    {
      key: "components",
      label: "Штук:",
      value: (data?.meta.componentCount ?? 0).toLocaleString("ru-RU"),
    },
    {
      key: "overdueAssembly",
      label: "Просрочена сборка:",
      value: (data?.meta.overdueAssemblyCount ?? 0).toLocaleString("ru-RU"),
      color: "error.main",
      hidden: !isLoading && !data?.meta.overdueAssemblyCount && overdue !== "assembly",
      onClick: () => setOverdue(overdue === "assembly" ? "" : "assembly"),
      active: overdue === "assembly",
    },
    {
      key: "overdueShipment",
      label: "Просрочена отгрузка:",
      value: (data?.meta.overdueShipmentCount ?? 0).toLocaleString("ru-RU"),
      color: "warning.main",
      hidden: !isLoading && !data?.meta.overdueShipmentCount && overdue !== "shipment",
      onClick: () => setOverdue(overdue === "shipment" ? "" : "shipment"),
      active: overdue === "shipment",
    },
  ];

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
  const selectionActions: BulkAction[] =
    selectedItems.length > 0
      ? [
          ...(bulkActions?.(selectedItems) ?? []),
          ...(canSelfAssign && selectedConfirmedIds.length > 0
            ? [
                {
                  key: "selfAssign",
                  label: "Взять на себя",
                  icon: <AssignmentIndIcon />,
                  count: selectedConfirmedIds.length,
                  primary: true,
                  pending: selfAssignMutation.isPending,
                  disabled: selfAssignMutation.isPending,
                  onClick: handleSelfAssignSelected,
                },
              ]
            : []),
          ...availableTransitions.map(({transition, count}) => ({
            key: transition.key,
            label: transition.label,
            icon: transition.icon,
            count,
            primary: transition.primary,
            danger: transition.danger,
            pending: transitionMutation.isPending && activeTransition?.key === transition.key,
            disabled: transitionMutation.isPending,
            onClick: () => handleTransitionClick(transition),
          })),
        ]
      : [];
  // чекбокс + сортируемые колонки + «Штук» + «Теги»
  const columnCount = sortColumns.length + 3 + (showNotes ? 1 : 0) + (extraColumns?.length ?? 0);

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
        <SearchWithItemsInput
          text={inputValue}
          onTextChange={setInputValue}
          itemIds={catalogItemIds}
          onItemIdsChange={setCatalogItemIds}
          sx={{flexGrow: 1}}
        />
      </PageGenericHeader>
      {showStatusFilter && (
        <OrderStatusTabs
          value={status}
          onChange={setStatus}
          statuses={ALL_STATUSES}
          counts={statusCounts ?? undefined}
        />
      )}
      <FiltersBar
        activeCount={
          [
            showWarehouseFilter && warehouseId,
            tagIds.length > 0,
            marketplaceFilters && (marketplaceType || marketplaceAccountId),
            marketplaceFilters && marketplaceStatus,
            marketplaceFilters && showExternalFilter && includeExternal !== defaultIncludeExternal,
          ].filter(Boolean).length
        }
        actions={
          marketplaceFilters &&
          showExternalFilter && (
            <Tooltip
              title={
                includeExternal
                  ? "Скрыть внешние заказы"
                  : "Показать внешние заказы — с площадки, не проходившие через WMS"
              }
            >
              <ToggleButton
                value="includeExternal"
                selected={includeExternal}
                onChange={() => setIncludeExternal(!includeExternal)}
                aria-label="Внешние заказы"
                // matches the 40px height of the size="small" inputs next to it
                sx={{width: 40, height: 40, p: 0}}
              >
                <AltRouteIcon fontSize="small" />
              </ToggleButton>
            </Tooltip>
          )
        }
      >
        {showWarehouseFilter && (
          <WarehousesSelect
            value={warehouseId}
            onChange={setWarehouseId}
            sx={{flexBasis: 200}}
            size="small"
            textFieldProps={{label: "Склад"}}
          />
        )}
        <DocumentTagsFilter
          kind="order"
          value={tagIds}
          onChange={setTagIds}
          sx={{minWidth: 220, maxWidth: 420, flexGrow: 1}}
        />
        {marketplaceFilters && (
          <MarketplaceOrderFilters
            type={marketplaceType}
            accountId={marketplaceAccountId}
            onSourceChange={handleMarketplaceSourceChange}
            status={marketplaceStatus}
            onStatusChange={setMarketplaceStatus}
          />
        )}
      </FiltersBar>

      {type == "fboSupply" && (
        <Alert severity={"warning"}>Раздел "Поставки FBO" еще не реализован</Alert>
      )}

      <BulkBar
        count={selectedItems.length}
        countLabel={{one: "заказ выбран", few: "заказа выбрано", many: "заказов выбрано"}}
        onClear={clear}
        actions={selectionActions}
        info={listStats}
        infoLoading={isLoading}
      />

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
              {extraColumns?.map(({key, label, align, noWrap}) => (
                <TableCell key={key} align={align} sx={noWrap ? {whiteSpace: "nowrap"} : undefined}>
                  {label}
                </TableCell>
              ))}
              {showNotes && <TableCell>Заметки</TableCell>}
              <TableCell>Штук</TableCell>
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
                  {!isHidden("number") && (
                    <TableCell sx={{fontFamily: "monospace"}}>
                      {formatOrderNumber(order.number)}
                    </TableCell>
                  )}
                  {!isHidden("status") && (
                    <TableCell>
                      <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}} useFlexGap>
                        <OrderStatusChip status={order.status} />
                        {order.isExternal && (
                          <Chip
                            size="small"
                            variant="outlined"
                            label="Внешний"
                            title="Заказ с площадки: через склад не проходил"
                          />
                        )}
                      </Stack>
                    </TableCell>
                  )}
                  {!isHidden("warehouseName") && (
                    <TableCell>{order.warehouseName ?? "—"}</TableCell>
                  )}
                  {!isHidden("plannedShipmentAt") && (
                    <DateTimeTableCell value={order.plannedShipmentAt} />
                  )}
                  {!isHidden("createdAt") && <DateTimeTableCell value={order.createdAt} />}
                  {statusDateColumns.map((column) => (
                    <DateTimeTableCell key={column.key} value={column.get(order)} />
                  ))}
                  {extraColumns?.map(({key, render, align, noWrap}) => (
                    <TableCell
                      key={key}
                      align={align}
                      sx={noWrap ? {whiteSpace: "nowrap"} : undefined}
                    >
                      {render(order)}
                    </TableCell>
                  ))}
                  {showNotes && (
                    <NotesTableCell notes={order.notes} sx={{position: "relative", zIndex: 1}} />
                  )}
                  <TableCell>
                    <OrderCompositionPreview order={order} />
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
