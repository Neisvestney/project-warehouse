import OrdersListPage from "@/components/orders/OrdersListPage";
import type {OrdersListExtraColumn} from "@/components/orders/OrdersListPage";
import {useDownloadLabelsAction} from "@/components/orders/marketplace/useDownloadLabelsAction";
import {useRebindOrdersAction} from "@/components/orders/marketplace/useRebindOrdersAction";
import MarketplaceOrderStatusChip from "@/components/orders/marketplace/MarketplaceOrderStatusChip";
import SyncOrdersButton from "@/components/orders/marketplace/SyncOrdersButton";
import {useHasPermission} from "@/hooks/usePermission";
import {formatPostingNumber, formatLabelBarcode} from "@/utils/postingNumberUtils";
import MarketplaceSupplyLabel from "@/components/orders/marketplace/MarketplaceSupplyLabel";
import {useSupplyFilterState} from "@/components/orders/marketplace/useSupplyFilterState";
import type {OrderSummaryDto} from "@/api/types.gen";
import {Tooltip, Typography} from "@mui/material";
import {DateTimeText} from "@/components/DateTimeTableCell";
import OrderCompositionPreview from "@/components/orders/OrderCompositionPreview";
import {MARKETPLACE_TYPE_LABELS} from "@/components/marketplace/marketplaceUtils";
import {MARKETPLACE_ORDER_RETURN_STATE_LABELS} from "@/components/orders/marketplace/marketplaceOrderUtils";

function SupplyCell({order}: {order: OrderSummaryDto}) {
  const [, setSupplyId] = useSupplyFilterState();
  const supply = order.marketplaceOrder?.supply;
  return supply ? <MarketplaceSupplyLabel supply={supply} onFilter={setSupplyId} /> : "—";
}

const EXTRA_COLUMNS: OrdersListExtraColumn[] = [
  {
    key: "labelFetchedAt",
    label: "Дата получения стикера",
    sortBy: "labelFetchedAt",
    defaultHidden: true,
    noWrap: true,
    render: (order) => <DateTimeText value={order.marketplaceOrder?.labelFetchedAt} />,
  },
  {
    key: "marketplaceStatus",
    label: "Статус на площадке",
    sortBy: "marketplaceStatus",
    defaultHidden: false,
    render: (order) => <MarketplaceOrderStatusChip value={order.marketplaceOrder} />,
  },
  {
    key: "postingNumber",
    label: "Номер отправления",
    sortBy: "postingNumber",
    defaultHidden: false,
    align: "right",
    noWrap: true,
    render: (order) => (
      <>
        <Typography variant="body2" sx={{fontFamily: "monospace"}}>
          {formatPostingNumber(order.marketplaceOrder?.postingNumber) ?? "—"}
        </Typography>
        {order.marketplaceOrder?.labelBarcode && (
          <Typography variant="caption" component="div" sx={{fontFamily: "monospace"}}>
            {formatLabelBarcode(order.marketplaceOrder.labelBarcode)}
          </Typography>
        )}
      </>
    ),
  },
  {
    key: "supply",
    label: "Поставка",
    sortBy: "supply",
    defaultHidden: false,
    render: (order) => <SupplyCell order={order} />,
  },
  {
    key: "composition",
    label: "Состав",
    defaultHidden: true,
    sortBy: "composition",
    render: (order) => <OrderCompositionPreview order={order} variant="composition" />,
  },
  {
    key: "marketplaceType",
    label: "Маркетплейс",
    defaultHidden: true,
    sortBy: "marketplaceType",
    render: (order) =>
      order.marketplaceOrder
        ? MARKETPLACE_TYPE_LABELS[order.marketplaceOrder.marketplaceType]
        : "—",
  },
  {
    key: "marketplaceAccountName",
    label: "Аккаунт продавца",
    defaultHidden: true,
    sortBy: "marketplaceAccountName",
    render: (order) => order.marketplaceOrder?.marketplaceAccountName ?? "—",
  },
  {
    key: "shipmentDate",
    label: "Срок отгрузки на площадке",
    defaultHidden: true,
    sortBy: "shipmentDate",
    noWrap: true,
    render: (order) => <DateTimeText value={order.marketplaceOrder?.shipmentDate} />,
  },
  {
    key: "assembledAt",
    label: "Собран",
    defaultHidden: true,
    sortBy: "assembledAt",
    noWrap: true,
    render: (order) => <DateTimeText value={order.assembledAt} />,
  },
  {
    key: "shippedAt",
    label: "Отгружен",
    defaultHidden: true,
    sortBy: "shippedAt",
    noWrap: true,
    render: (order) => <DateTimeText value={order.shippedAt} />,
  },
  {
    key: "statusSyncedAt",
    label: "Последняя синхронизация",
    defaultHidden: true,
    sortBy: "statusSyncedAt",
    noWrap: true,
    render: (order) => <DateTimeText value={order.marketplaceOrder?.statusSyncedAt} />,
  },
  {
    key: "returnState",
    label: "Возврат",
    defaultHidden: true,
    sortBy: "returnState",
    render: (order) =>
      order.marketplaceOrder
        ? MARKETPLACE_ORDER_RETURN_STATE_LABELS[order.marketplaceOrder.returnState]
        : "—",
  },
  {
    key: "cancelReason",
    label: "Причина отмены",
    defaultHidden: true,
    sortBy: "cancelReason",
    render: (order) => order.marketplaceOrder?.cancelReason || "—",
  },
  {
    key: "notes",
    label: "Заметки",
    defaultHidden: true,
    sortBy: "notes",
    render: (order) => (
      <Tooltip title={order.notes ?? ""}>
        <Typography variant="body2" noWrap sx={{maxWidth: 200, position: "relative", zIndex: 1}}>
          {order.notes || "—"}
        </Typography>
      </Tooltip>
    ),
  },
];

function OrdersFbsPage() {
  const canSync = useHasPermission("integrations.sync");
  const canEdit = useHasPermission(["orders.edit", "orders.edit_assigned"]);
  const downloadLabels = useDownloadLabelsAction();
  const rebind = useRebindOrdersAction();

  return (
    <>
      <OrdersListPage
        type="fbs"
        title="Заказы FBS"
        breadcrumbName="FBS"
        breadcrumbLink="/operations/orders/fbs"
        headerActions={
          canSync ? (
            <>
              <SyncOrdersButton statusesOnly />
              <SyncOrdersButton />
            </>
          ) : null
        }
        bulkActions={(orders) => {
          const rebindAction = canEdit ? rebind.getAction(orders) : null;
          return [
            downloadLabels.getAction(orders.map((o) => o.id)),
            ...(rebindAction ? [rebindAction] : []),
          ];
        }}
        marketplaceFilters
        supplyFilter
        extraColumns={EXTRA_COLUMNS}
        showNotes={false}
        defaultPageSize={200}
      />
      {downloadLabels.dialogs}
      {rebind.dialogs}
    </>
  );
}

export default OrdersFbsPage;
