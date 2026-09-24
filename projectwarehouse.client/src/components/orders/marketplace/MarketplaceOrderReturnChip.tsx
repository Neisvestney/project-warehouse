import {Chip} from "@mui/material";
import type {MarketplaceOrderReturnState} from "@/api/types.gen";
import {
  MARKETPLACE_ORDER_RETURN_STATE_COLORS,
  MARKETPLACE_ORDER_RETURN_STATE_LABELS,
} from "./marketplaceOrderUtils";

interface MarketplaceOrderReturnChipProps {
  value: MarketplaceOrderReturnState;
}

function MarketplaceOrderReturnChip({value}: MarketplaceOrderReturnChipProps) {
  if (value === "none") return null;
  return (
    <Chip
      size="small"
      label={MARKETPLACE_ORDER_RETURN_STATE_LABELS[value]}
      color={MARKETPLACE_ORDER_RETURN_STATE_COLORS[value]}
    />
  );
}

export default MarketplaceOrderReturnChip;
