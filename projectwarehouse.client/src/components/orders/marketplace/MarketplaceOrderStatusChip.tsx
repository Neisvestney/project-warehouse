import {Chip, Tooltip} from "@mui/material";
import type {MarketplaceOrderDto} from "@/api/types.gen";
import {
  MARKETPLACE_ORDER_STATUS_COLORS,
  MARKETPLACE_ORDER_STATUS_LABELS,
} from "./marketplaceOrderUtils";

interface MarketplaceOrderStatusChipProps {
  value?: MarketplaceOrderDto | null;
}

function MarketplaceOrderStatusChip({value}: MarketplaceOrderStatusChipProps) {
  if (!value) return <>—</>;

  const chip = (
    <Chip
      size="small"
      label={MARKETPLACE_ORDER_STATUS_LABELS[value.status]}
      color={MARKETPLACE_ORDER_STATUS_COLORS[value.status]}
    />
  );

  const tooltip =
    value.status === "cancelled"
      ? "Отменён на маркетплейсе — решение по заказу принимает человек"
      : (value.rawStatus ?? "");

  return (
    <Tooltip title={tooltip} disableHoverListener={!tooltip}>
      <span>{chip}</span>
    </Tooltip>
  );
}

export default MarketplaceOrderStatusChip;
