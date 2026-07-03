import {Chip} from "@mui/material";
import type {OrderStatus} from "@/api/types.gen";
import {ORDER_STATUS_COLORS, ORDER_STATUS_LABELS} from "./orderUtils";

interface OrderStatusChipProps {
  status: OrderStatus;
}

function OrderStatusChip({status}: OrderStatusChipProps) {
  return (
    <Chip label={ORDER_STATUS_LABELS[status]} color={ORDER_STATUS_COLORS[status]} size="small" />
  );
}

export default OrderStatusChip;
