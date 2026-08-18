import type {ChipProps} from "@mui/material";
import {Chip} from "@mui/material";
import type {StocktakeStatus} from "@/api/types.gen";
import {STOCKTAKE_STATUS_LABELS} from "@/components/stocktakes/stocktakeUtils";

const STATUS_COLORS: Record<StocktakeStatus, ChipProps["color"]> = {
  planned: "warning",
  draft: "default",
  inProgress: "info",
  finished: "success",
  canceled: "error",
};

interface StocktakeStatusChipProps {
  status: StocktakeStatus;
}

function StocktakeStatusChip({status}: StocktakeStatusChipProps) {
  return (
    <Chip label={STOCKTAKE_STATUS_LABELS[status]} color={STATUS_COLORS[status]} size="small" />
  );
}

export default StocktakeStatusChip;
