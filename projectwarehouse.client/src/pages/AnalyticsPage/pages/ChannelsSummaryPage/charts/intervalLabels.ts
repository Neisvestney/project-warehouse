import type {AnalyticsIntervalDto, AnalyticsStep, ChannelSeriesDto} from "@/api/types.gen";
import {parseDateOnly} from "@/utils/dateOnly";

function dayMonth(value: string) {
  return parseDateOnly(value).toLocaleDateString("ru-RU", {day: "numeric", month: "short"});
}

function monthLabel(value: string, withYear: boolean) {
  const date = parseDateOnly(value);
  const month = date.toLocaleDateString("ru-RU", {month: "short"}).replace(".", "");
  return withYear ? `${month} ${date.getFullYear()}` : month;
}

/**
 * Axis and column label of an interval; an unfinished one is starred so a short bar is not read as a drop.
 * A month carries its year only where the year starts — on the first interval and on January.
 */
export function intervalLabel(
  interval: AnalyticsIntervalDto,
  step: AnalyticsStep,
  isFirst = false,
): string {
  const label =
    step === "day"
      ? dayMonth(interval.start)
      : step === "week"
        ? `${dayMonth(interval.start)} – ${dayMonth(interval.end)}`
        : monthLabel(interval.start, isFirst || parseDateOnly(interval.start).getMonth() === 0);
  return interval.isCurrent ? `${label}*` : label;
}

/** Edges drawn dashed: an interval cut by the period or one that is still running. */
export function isIncomplete(interval: AnalyticsIntervalDto): boolean {
  return interval.isPartial || interval.isCurrent;
}

export function seriesLabel(series: Pick<ChannelSeriesDto, "kind" | "name">): string {
  return series.kind === "direct" ? "Прямые" : (series.name ?? "");
}
