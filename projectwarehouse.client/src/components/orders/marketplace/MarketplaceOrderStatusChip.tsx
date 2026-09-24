import {Chip, Tooltip} from "@mui/material";
import type {MarketplaceOrderDto} from "@/api/types.gen";
import {
  MARKETPLACE_ORDER_STATUS_COLORS,
  MARKETPLACE_ORDER_STATUS_LABELS,
} from "./marketplaceOrderUtils";
import MarketplaceOrderReturnChip from "./MarketplaceOrderReturnChip";

interface MarketplaceOrderStatusChipProps {
  value?: MarketplaceOrderDto | null;
  /** Show the marketplace status as is; the caller renders the return separately. */
  ignoreReturn?: boolean;
}

function MarketplaceOrderStatusChip({value, ignoreReturn}: MarketplaceOrderStatusChipProps) {
  if (!value) return <>—</>;

  const isCancelled = value.status === "cancelled";
  const showReturn = !ignoreReturn && !isCancelled && value.returnState !== "none";

  let label = MARKETPLACE_ORDER_STATUS_LABELS[value.status];
  if (isCancelled && !!value.cancelledAfterShip) {
    label = `${label} ${value.cancelledAfterShip ? "после огрузки" : "до огрузки"}`;
  }

  const chip = showReturn ? (
    <MarketplaceOrderReturnChip value={value.returnState} />
  ) : (
    <Chip size="small" label={label} color={MARKETPLACE_ORDER_STATUS_COLORS[value.status]} />
  );

  const tooltip = isCancelled
    ? "Отменён на маркетплейсе — решение по заказу принимает человек"
    : showReturn
      ? `Статус на площадке: ${label}`
      : (value.rawStatus ?? "");

  return (
    <Tooltip title={tooltip} disableHoverListener={!tooltip}>
      <span>{chip}</span>
    </Tooltip>
  );
}

export default MarketplaceOrderStatusChip;
