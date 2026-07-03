import OrdersListPage from "@/components/orders/OrdersListPage";

function OrdersFbsPage() {
  return (
    <OrdersListPage
      type="fbs"
      title="Заказы FBS"
      breadcrumbName="FBS"
      breadcrumbLink="/operations/orders/fbs"
    />
  );
}

export default OrdersFbsPage;
