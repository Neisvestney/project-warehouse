import OrdersListPage from "@/components/orders/OrdersListPage";
import {useOrderAnalyticsExclusionBulkActions} from "@/components/orders/useOrderAnalyticsExclusionBulkActions";

function OrdersDirectPage() {
  const analyticsExclusion = useOrderAnalyticsExclusionBulkActions();

  return (
    <OrdersListPage
      type="direct"
      title="Прямые заказы"
      breadcrumbName="Прямые"
      breadcrumbLink="/operations/orders/direct"
      createLink="/operations/orders/direct/new"
      bulkActions={analyticsExclusion.getActions}
    />
  );
}

export default OrdersDirectPage;
