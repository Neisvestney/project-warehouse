import OrdersListPage from "@/components/orders/OrdersListPage";

function OrdersFboPage() {
  return (
    <OrdersListPage
      type="fbo"
      title="Поставки FBO"
      breadcrumbName="FBO"
      breadcrumbLink="/operations/orders/fbo"
      marketplaceFilters
    />
  );
}

export default OrdersFboPage;
