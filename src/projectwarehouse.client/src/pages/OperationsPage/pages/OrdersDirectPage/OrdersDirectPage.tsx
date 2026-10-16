import OrdersListPage from "@/components/orders/OrdersListPage";

function OrdersDirectPage() {
  return (
    <OrdersListPage
      type="direct"
      title="Прямые заказы"
      breadcrumbName="Прямые"
      breadcrumbLink="/operations/orders/direct"
      createLink="/operations/orders/direct/new"
    />
  );
}

export default OrdersDirectPage;
