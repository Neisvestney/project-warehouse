import OrdersListPage from "@/components/orders/OrdersListPage";
import type {OrdersListExtraColumn} from "@/components/orders/OrdersListPage";
import DownloadLabelsButton from "@/components/orders/marketplace/DownloadLabelsButton";
import MarketplaceOrderStatusChip from "@/components/orders/marketplace/MarketplaceOrderStatusChip";
import SyncOrdersButton from "@/components/orders/marketplace/SyncOrdersButton";
import {useHasPermission} from "@/hooks/usePermission";

const EXTRA_COLUMNS: OrdersListExtraColumn[] = [
  {
    key: "marketplaceStatus",
    label: "Статус на площадке",
    render: (order) => <MarketplaceOrderStatusChip value={order.marketplaceOrder} />,
  },
];

function OrdersFbsPage() {
  const canSync = useHasPermission("integrations.map");

  return (
    <OrdersListPage
      type="fbs"
      title="Заказы FBS"
      breadcrumbName="FBS"
      breadcrumbLink="/operations/orders/fbs"
      headerActions={canSync ? <SyncOrdersButton /> : null}
      bulkActions={(ids) => <DownloadLabelsButton orderIds={ids} />}
      extraColumns={EXTRA_COLUMNS}
    />
  );
}

export default OrdersFbsPage;
