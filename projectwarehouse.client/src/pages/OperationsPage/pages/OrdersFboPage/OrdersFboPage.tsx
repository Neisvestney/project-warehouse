import {Typography} from "@mui/material";
import OrdersListPage from "@/components/orders/OrdersListPage";
import type {OrdersListExtraColumn} from "@/components/orders/OrdersListPage";

function OrdersFboPage() {
  return (
    <OrdersListPage
      type="fbo"
      title="Заказы FBO"
      breadcrumbName="FBO"
      breadcrumbLink="/operations/orders/fbo"
      marketplaceFilters
    />
  );
}

export default OrdersFboPage;
