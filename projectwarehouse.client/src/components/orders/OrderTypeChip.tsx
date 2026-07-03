import {Chip} from "@mui/material";
import type {OrderType} from "@/api/types.gen";
import {ORDER_TYPE_COLORS, ORDER_TYPE_LABELS} from "./orderUtils";

interface OrderTypeChipProps {
  type: OrderType;
}

function OrderTypeChip({type}: OrderTypeChipProps) {
  return <Chip label={ORDER_TYPE_LABELS[type]} color={ORDER_TYPE_COLORS[type]} size="small" />;
}

export default OrderTypeChip;
