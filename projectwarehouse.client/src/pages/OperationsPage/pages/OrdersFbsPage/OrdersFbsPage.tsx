import OrdersListPage from "@/components/orders/OrdersListPage";
import type {OrdersListExtraColumn} from "@/components/orders/OrdersListPage";
import {useDownloadLabelsAction} from "@/components/orders/marketplace/useDownloadLabelsAction";
import {useRebindOrdersAction} from "@/components/orders/marketplace/useRebindOrdersAction";
import MarketplaceOrderStatusChip from "@/components/orders/marketplace/MarketplaceOrderStatusChip";
import SyncOrdersButton from "@/components/orders/marketplace/SyncOrdersButton";
import {useHasPermission} from "@/hooks/usePermission";
import {formatPostingNumber, formatScanitBarcode} from "@/utils/postingNumberUtils";
import {Typography} from "@mui/material";

const EXTRA_COLUMNS: OrdersListExtraColumn[] = [
  {
    key: "marketplaceStatus",
    label: "Статус на площадке",
    render: (order) => <MarketplaceOrderStatusChip value={order.marketplaceOrder} />,
  },
  {
    key: "postingNumber",
    label: "Номер отправления",
    align: "right",
    noWrap: true,
    render: (order) => (
      <>
        <Typography variant="body2" sx={{fontFamily: "monospace"}}>
          {formatPostingNumber(order.marketplaceOrder?.postingNumber) ?? "—"}
        </Typography>
        {order.marketplaceOrder?.scanitBarcode && (
          <Typography variant="caption" component="div" sx={{fontFamily: "monospace"}}>
            {formatScanitBarcode(order.marketplaceOrder.scanitBarcode)}
          </Typography>
        )}
      </>
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
        headerActions={canSync ? <SyncOrdersButton /> : null}
        bulkActions={(orders) => {
          const rebindAction = canEdit ? rebind.getAction(orders) : null;
          return [
            downloadLabels.getAction(orders.map((o) => o.id)),
            ...(rebindAction ? [rebindAction] : []),
          ];
        }}
        marketplaceFilters
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
