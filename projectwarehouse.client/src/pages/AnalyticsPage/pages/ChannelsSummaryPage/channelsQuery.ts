import type {AnalyticsMeasure, AnalyticsStep} from "@/api/types.gen";

/** The Direct channel inside the channel selection; every other entry is a marketplace account id. */
export const DIRECT_CHANNEL = "direct";

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
const STEPS: AnalyticsStep[] = ["day", "week", "month"];

export function parseDate(query: string | null): string | null {
  return query && DATE_PATTERN.test(query) ? query : null;
}

export function parseList(query: string | null): string[] {
  return query ? query.split(",").filter(Boolean) : [];
}

export function serializeList(value: string[]): string | null {
  return value.length > 0 ? value.join(",") : null;
}

/** Null leaves the step to the server, which picks it by the period length. */
export function parseStep(query: string | null): AnalyticsStep | null {
  return STEPS.includes(query as AnalyticsStep) ? (query as AnalyticsStep) : null;
}

export function parseMeasure(query: string | null): AnalyticsMeasure {
  return query === "units" ? "units" : "orders";
}

/** Empty selection means every channel; the flags let «Прямые» alone be asked for. */
export function channelSelectionQuery(channels: string[]) {
  const all = channels.length === 0;
  const accountIds = channels.filter((c) => c !== DIRECT_CHANNEL);
  return {
    IncludeMarketplaces: all || accountIds.length > 0,
    MarketplaceAccountIds: all ? undefined : accountIds,
    IncludeDirect: all || channels.includes(DIRECT_CHANNEL),
  };
}
