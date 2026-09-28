import {addDays, parseDateOnly, toDateOnly, todayDateOnly} from "@/utils/dateOnly";

export type CalendarPreset = "month" | "quarter" | "year";
export type RollingPreset = "lastMonth" | "lastYear";
export type PeriodPreset = "page" | CalendarPreset | RollingPreset | "allTime" | "custom";

/**
 * A period picked on its own, apart from the page filter; `page` follows the page. `allTime` has no dates of
 * its own and resolves to the bounds the caller passes as `page`.
 */
export type PeriodSelection =
  | {preset: "page"}
  | {preset: CalendarPreset; anchor: string}
  | {preset: RollingPreset}
  | {preset: "allTime"}
  | {preset: "custom"; from: string; to: string};

export interface Period {
  from: string;
  to: string;
}

export const PERIOD_PRESET_LABELS: Record<PeriodPreset, string> = {
  page: "Как у страницы",
  month: "Месяц",
  quarter: "Квартал",
  year: "Год",
  lastMonth: "Последний месяц",
  lastYear: "Последний год",
  allTime: "За всё время",
  custom: "Произвольный",
};

/** Presets of a period that stands on its own: the page filter and a fullscreen chart. */
export const PERIOD_PRESETS = [
  "month",
  "quarter",
  "year",
  "lastMonth",
  "lastYear",
  "custom",
] as const;

/** Mirrors `AnalyticsCalculator.MaxPeriodDays` on the server. */
const MAX_PERIOD_DAYS = 366;

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const CALENDAR_PRESETS: string[] = ["month", "quarter", "year"];
const ROLLING_PRESETS: string[] = ["lastMonth", "lastYear"];

export function isCalendarPreset(preset: PeriodPreset): preset is CalendarPreset {
  return CALENDAR_PRESETS.includes(preset);
}

/** First day of the calendar period holding `date`. */
function periodStart(preset: CalendarPreset, date: string): string {
  const d = parseDateOnly(date);
  if (preset === "year") return toDateOnly(new Date(d.getFullYear(), 0, 1));
  const month = preset === "quarter" ? Math.floor(d.getMonth() / 3) * 3 : d.getMonth();
  return toDateOnly(new Date(d.getFullYear(), month, 1));
}

function addMonths(date: string, months: number): string {
  const d = parseDateOnly(date);
  const target = new Date(d.getFullYear(), d.getMonth() + months, 1);
  const lastDay = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
  target.setDate(Math.min(d.getDate(), lastDay));
  return toDateOnly(target);
}

const CALENDAR_MONTHS: Record<CalendarPreset, number> = {month: 1, quarter: 3, year: 12};

/**
 * URL form: `page` is no param at all, `month:2026-09-01` keeps the period's first day, `lastYear`, `allTime`,
 * `custom:2026-01-01_2026-03-31`. Anything unreadable falls back to `fallback`.
 */
export function parsePeriod(query: string | null, fallback: PeriodSelection): PeriodSelection {
  if (!query) return fallback;
  const [preset, rest] = query.split(":");
  if (ROLLING_PRESETS.includes(preset)) return {preset: preset as RollingPreset};
  if (preset === "allTime") return {preset};
  if (CALENDAR_PRESETS.includes(preset) && rest && DATE_PATTERN.test(rest))
    return {preset: preset as CalendarPreset, anchor: periodStart(preset as CalendarPreset, rest)};
  if (preset === "custom" && rest) {
    const [from, to] = rest.split("_");
    if (DATE_PATTERN.test(from) && DATE_PATTERN.test(to)) return {preset, from, to};
  }
  return fallback;
}

export function serializePeriod(value: PeriodSelection): string | null {
  switch (value.preset) {
    case "page":
      return null;
    case "custom":
      return `custom:${value.from}_${value.to}`;
    case "lastMonth":
    case "lastYear":
    case "allTime":
      return value.preset;
    default:
      return `${value.preset}:${value.anchor}`;
  }
}

/** Rolling presets end today and span one month or one year counting today in. */
export function resolvePeriod(value: PeriodSelection, page: Period): Period {
  switch (value.preset) {
    case "page":
    case "allTime":
      return page;
    case "custom":
      return {from: value.from, to: value.to};
    case "lastMonth":
    case "lastYear": {
      const today = todayDateOnly();
      return {
        from: addDays(addMonths(today, value.preset === "lastYear" ? -12 : -1), 1),
        to: today,
      };
    }
    default:
      return {
        from: value.anchor,
        to: addDays(addMonths(value.anchor, CALENDAR_MONTHS[value.preset]), -1),
      };
  }
}

/** Switches the preset, starting from where the current period is so the view does not jump. */
export function withPreset(preset: PeriodPreset, current: Period): PeriodSelection {
  if (preset === "page") return {preset};
  if (preset === "custom") {
    // «За всё время» can span more than the server accepts for dated periods
    const earliest = addDays(current.to, 1 - MAX_PERIOD_DAYS);
    return {preset, from: current.from < earliest ? earliest : current.from, to: current.to};
  }
  if (isCalendarPreset(preset)) return {preset, anchor: periodStart(preset, current.to)};
  return {preset};
}

export function shiftPeriod(value: PeriodSelection, direction: -1 | 1): PeriodSelection {
  if (!isCalendarPreset(value.preset) || !("anchor" in value)) return value;
  return {
    preset: value.preset,
    anchor: addMonths(value.anchor, direction * CALENDAR_MONTHS[value.preset]),
  };
}

export function periodLabel(value: PeriodSelection, period: Period): string {
  if (value.preset === "year") return String(parseDateOnly(value.anchor).getFullYear());
  if (value.preset === "quarter") {
    const date = parseDateOnly(value.anchor);
    return `${["I", "II", "III", "IV"][Math.floor(date.getMonth() / 3)]} квартал ${date.getFullYear()}`;
  }
  if (value.preset === "month") {
    const month = parseDateOnly(value.anchor).toLocaleDateString("ru-RU", {
      month: "long",
      year: "numeric",
    });
    return month.charAt(0).toUpperCase() + month.slice(1).replace(" г.", "");
  }
  // The start carries its year only when the period crosses into the next one
  const sameYear = period.from.slice(0, 4) === period.to.slice(0, 4);
  return `${shortDate(period.from, !sameYear)} — ${shortDate(period.to, true)}`;
}

function shortDate(value: string, withYear: boolean): string {
  const [year, month, day] = value.split("-");
  return withYear ? `${day}.${month}.${year}` : `${day}.${month}`;
}
