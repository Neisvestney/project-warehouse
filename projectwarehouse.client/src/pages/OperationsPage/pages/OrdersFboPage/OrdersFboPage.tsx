import {Typography} from "@mui/material";
import OrdersListPage from "@/components/orders/OrdersListPage";
import type {OrdersListExtraColumn} from "@/components/orders/OrdersListPage";
import MarketplaceOrderStatusChip from "@/components/orders/marketplace/MarketplaceOrderStatusChip";
import {formatPostingNumber} from "@/utils/postingNumberUtils";
import type {OrderSortBy, OrderStatus} from "@/api/types.gen";

// склад ведёт площадка, так что внутренний статус, склад и плановая отгрузка здесь пустуют
const HIDDEN_COLUMNS: OrderSortBy[] = ["status", "warehouseName", "plannedShipmentAt"];

const STATUS_DATES: OrderStatus[] = ["shipped"];

// на FBO отметка отгрузки — момент, когда площадка взяла отправление в работу
const STATUS_DATE_LABELS: Partial<Record<OrderStatus, string>> = {shipped: "Оформлен"};

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
      <Typography variant="body2" sx={{fontFamily: "monospace"}}>
        {formatPostingNumber(order.marketplaceOrder?.postingNumber) ?? "—"}
      </Typography>
    ),
  },
];

function OrdersFboPage() {
  return (
    <OrdersListPage
      type="fboPosting"
      title="Отправления FBO"
      breadcrumbName="Отправления FBO"
      breadcrumbLink="/operations/orders/fbo"
      marketplaceFilters
      // площадка отгружает их со своего склада, так что внешними они являются все до одного
      defaultIncludeExternal
      showExternalFilter={false}
      hiddenColumns={HIDDEN_COLUMNS}
      alwaysShownStatusDates={STATUS_DATES}
      statusDateLabels={STATUS_DATE_LABELS}
      extraColumns={EXTRA_COLUMNS}
      showNotes={false}
      defaultPageSize={200}
    />
  );
}

export default OrdersFboPage;
