import {Chip, type ChipProps} from "@mui/material";
import type {StockForecastDto, StockForecastStatus} from "@/api/types.gen";

const STATUS_COLOR: Record<StockForecastStatus, ChipProps["color"]> = {
  outOfStock: "error",
  warning: "warning",
  ok: "success",
  noConsumption: "default",
};

interface StockForecastChipProps extends Omit<ChipProps, "label" | "color"> {
  forecast: StockForecastDto;
}

// The status is authoritative — never re-derive it from daysLeft here. A null daysLeft is
// "never runs out", and `null <= warningDays` is true in JS, so comparing would paint «∞» as a warning.
function label(forecast: StockForecastDto): string {
  if (forecast.status === "outOfStock") return "Нет в наличии";
  if (forecast.daysLeft == null) return "∞";
  return `${forecast.daysLeft} дн.`;
}

export function StockForecastChip({
  forecast,
  size = "small",
  ...chipProps
}: StockForecastChipProps) {
  return (
    <Chip
      {...chipProps}
      size={size}
      color={STATUS_COLOR[forecast.status]}
      label={label(forecast)}
    />
  );
}

export default StockForecastChip;
