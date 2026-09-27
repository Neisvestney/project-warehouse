import type {ChipProps} from "@mui/material";
import type {AbcClass, AnalyticsAbcBasis, XyzClass} from "@/api/types.gen";
import {formatMoney, formatNumber} from "@/components/analytics/analyticsFormat";

export const ABC_CLASSES: AbcClass[] = ["a", "b", "c"];
export const XYZ_CLASSES: XyzClass[] = ["x", "y", "z"];

export const ABC_COLORS: Record<AbcClass, ChipProps["color"]> = {
  a: "success",
  b: "warning",
  c: "default",
};

export const XYZ_COLORS: Record<XyzClass, ChipProps["color"]> = {
  x: "info",
  y: "secondary",
  z: "default",
};

export const BASIS_LABELS: Record<AnalyticsAbcBasis, string> = {
  units: "Штуки",
  price: "Цена продажи",
  payout: "Выплата",
};

export function formatAbcValue(
  value: number,
  basis: AnalyticsAbcBasis,
  currencyCode: string | null | undefined,
) {
  return basis === "units" || !currencyCode
    ? formatNumber(value)
    : formatMoney(value, currencyCode);
}

const boundaryFormat = new Intl.NumberFormat("ru-RU", {maximumFractionDigits: 1});

/** Settings boundaries arrive as percents, not fractions. */
export function formatBoundary(percent: number): string {
  return `${boundaryFormat.format(percent)}%`;
}
