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

  let label = MARKETPLACE_ORDER_STATUS_LABELS[value.status];
  if (value.status === "cancelled" && !!value.cancelledAfterShip) {
    label = `${label} ${value.cancelledAfterShip ? "после огрузки" : "до огрузки"}`;
  }

  const chip = (
    <Chip size="small" label={label} color={MARKETPLACE_ORDER_STATUS_COLORS[value.status]} />
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
