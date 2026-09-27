import {Chip} from "@mui/material";
import type {MarketplaceType} from "@/api/types.gen";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import {ORDER_TYPE_COLORS} from "@/components/orders/orderUtils";
import {useHasPermission} from "@/hooks/usePermission";
import {type ChannelRef, channelLabel} from "./channelsSummaryUtils";

// Duplicated from the settings tree on purpose: analytics must not import from it
const MARKETPLACE_CHIP_COLORS: Record<MarketplaceType, "ozon" | "wb"> = {
  ozon: "ozon",
  wildberries: "wb",
};

function ChannelChip({row}: {row: ChannelRef}) {
  // Without the right the integration page answers «Нет доступа», so the chip stays a plain label
  const canOpenIntegration = useHasPermission("integrations.view");

  if (row.kind === "marketplace" && row.marketplaceAccountId && row.marketplaceType)
    return canOpenIntegration ? (
      <MarketplaceAccountChip
        accountId={row.marketplaceAccountId}
        name={channelLabel(row)}
        type={row.marketplaceType}
        clickable
      />
    ) : (
      <Chip
        size="small"
        label={channelLabel(row)}
        color={MARKETPLACE_CHIP_COLORS[row.marketplaceType]}
      />
    );
  if (row.kind === "direct")
    return <Chip size="small" label={channelLabel(row)} color={ORDER_TYPE_COLORS.direct} />;
  return <Chip size="small" variant="outlined" label={channelLabel(row)} />;
}

export default ChannelChip;
