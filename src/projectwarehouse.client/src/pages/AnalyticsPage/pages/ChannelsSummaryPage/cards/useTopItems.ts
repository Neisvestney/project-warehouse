import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsTopItemsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsTopItemsBy} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {DIRECT_CHANNEL} from "@/components/analytics/channelsQuery";
import {useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import {useCardChannel} from "./useCardChannel";

export const TOP_ITEMS_SHOWN = 10;

/**
 * State of the top items, set on the card and read by its full list too: the channel, the ranking, the
 * currency and the card's own period, all kept in the URL.
 */
export function useTopItemsState(filters: ReturnType<typeof useChannelsSummaryFilters>) {
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("topperiod", pagePeriod);
  const cardChannel = useCardChannel(filters, "topch");
  const [storedBy, setBy] = useSyncedWithQueryState<AnalyticsTopItemsBy>(
    "topby",
    (q) => (q === "money" ? "money" : "units"),
    (v) => (v === "money" ? v : null),
  );
  const [currency, setCurrency] = useSyncedWithQueryState<string | null>(
    "topcur",
    (q) => q || null,
    (v) => v,
  );

  const channel = cardChannel.channel;
  // Money lives on shop lines only; the page may have narrowed «all channels» down to Direct alone
  const canRankByMoney = channel !== DIRECT_CHANNEL && filters.query.IncludeMarketplaces;
  const by: AnalyticsTopItemsBy = canRankByMoney ? storedBy : "units";

  const query = {
    ...filters.query,
    ...cardChannel.query,
    From: period.from,
    To: period.to,
    By: by,
    CurrencyCode: currency ?? undefined,
  };

  return {
    query,
    options: cardChannel.options,
    channel,
    channelLabel: cardChannel.label,
    canRankByMoney,
    by,
    currency,
    selection,
    period,
    pagePeriod,
    setChannel: cardChannel.setChannel,
    setBy,
    setCurrency,
    setSelection,
  };
}

export function useTopItemsQuery(
  state: ReturnType<typeof useTopItemsState>,
  take: number | undefined,
  enabled = true,
) {
  return useQuery({
    ...analyticsGetChannelsTopItemsOptions({query: {...state.query, Take: take}}),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
    enabled,
  });
}
