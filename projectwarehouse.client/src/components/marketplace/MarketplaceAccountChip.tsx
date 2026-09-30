import type {MouseEvent} from "react";
import {Chip, type ChipProps} from "@mui/material";
import {Link} from "react-router";
import type {MarketplaceType} from "@/api/types.gen";
import {MARKETPLACE_TYPE_COLORS} from "@/components/marketplace/marketplaceUtils.ts";

interface MarketplaceAccountChipProps extends Omit<ChipProps, "label" | "color"> {
  accountId: string;
  name: string;
  type: MarketplaceType;
  /** Appended to the integration link, e.g. "?tab=warehouses". */
  search?: string;
}

function MarketplaceAccountChip({
  accountId,
  name,
  type,
  search = "",
  ...chipProps
}: MarketplaceAccountChipProps) {
  return (
    <Chip
      component={Link}
      to={`/marketplaces/${accountId}${search}`}
      size="small"
      label={name}
      color={MARKETPLACE_TYPE_COLORS[type]}
      onClick={(e: MouseEvent<HTMLElement>) => e.stopPropagation()}
      {...chipProps}
    />
  );
}

export default MarketplaceAccountChip;
