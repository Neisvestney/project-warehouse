import type {ChannelSeriesDto} from "@/api/types.gen";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";

/** A channel line: counts straight from the server, or shares derived from two of them. */
export type ChartSeries = Omit<ChannelSeriesDto, "total"> & {total: number | null};

export type ValueFormat = (value: number) => string;

export const formatCount: ValueFormat = formatNumber;
export const formatShare: ValueFormat = (value) => formatPercent(value);

function ratio(part: number | null | undefined, whole: number | null | undefined) {
  return part == null || !whole ? null : part / whole;
}

/** Each part series divided by the whole of the same channel, interval by interval and in total. */
export function shareSeries(parts: ChannelSeriesDto[], wholes: ChannelSeriesDto[]): ChartSeries[] {
  return parts.map((part, i) => {
    const whole = wholes[i];
    return {
      ...part,
      values: part.values.map((value, j) => ratio(value, whole?.values[j])),
      total: ratio(part.total, whole?.total),
    };
  });
}
