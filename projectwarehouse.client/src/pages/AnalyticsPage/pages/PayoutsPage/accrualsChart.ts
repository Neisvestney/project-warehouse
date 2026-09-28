import type {PayoutsCurrencySeriesDto, PayoutsTimeseriesDto} from "@/api/types.gen";
import {formatMoney} from "@/components/analytics/analyticsFormat";
import type {ChartData} from "@/components/analytics/charts/ChartFullscreenDialog";
import type {ValueFormat} from "@/components/analytics/charts/chartSeries";

export const ACCRUALS_TITLE = "Динамика начислений";

/** One currency block of the response as the channel line chart reads it. */
export function toChartData(
  data: PayoutsTimeseriesDto,
  block: PayoutsCurrencySeriesDto,
): ChartData {
  return {
    intervals: data.intervals,
    step: data.step,
    timeZoneId: data.timeZoneId,
    series: block.series.map((s) => ({kind: "marketplace", ...s})),
  };
}

export function moneyFormat(currencyCode: string): ValueFormat {
  return (value) => formatMoney(value, currencyCode);
}
