import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {MarketplaceSyncStatus} from "@/api/types.gen";
import {SYNC_STATUS_LABELS} from "../marketplaceUtils";

const STATUS_COLORS: Record<MarketplaceSyncStatus, ChipProps["color"]> = {
  running: "info",
  success: "success",
  failed: "error",
  canceled: "default",
};

interface MarketplaceStatusChipProps {
  status: MarketplaceSyncStatus | null | undefined;
}

function MarketplaceStatusChip({status}: MarketplaceStatusChipProps) {
  if (!status) return <Chip label="Не синхронизировался" size="small" />;
  return <Chip label={SYNC_STATUS_LABELS[status]} color={STATUS_COLORS[status]} size="small" />;
}

export default MarketplaceStatusChip;
