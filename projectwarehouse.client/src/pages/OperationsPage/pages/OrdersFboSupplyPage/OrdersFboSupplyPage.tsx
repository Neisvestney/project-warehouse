import OrdersListPage from "@/components/orders/OrdersListPage";

function OrdersFboSupplyPage() {
  return (
    <OrdersListPage
      type="fboSupply"
      title="Поставки FBO"
      breadcrumbName="Поставки FBO"
      breadcrumbLink="/operations/orders/fbo-supply"
      marketplaceFilters
    />
  );
}

export default OrdersFboSupplyPage;
