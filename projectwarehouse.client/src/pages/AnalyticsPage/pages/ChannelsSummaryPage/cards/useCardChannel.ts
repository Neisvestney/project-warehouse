import {useQuery} from "@tanstack/react-query";
import {marketplacesGetAccountsShortOptions} from "@/api/@tanstack/react-query.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {DIRECT_CHANNEL} from "../channelsQuery";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

export interface CardChannelOption {
  /** Null is every channel the page has selected. */
  value: string | null;
  label: string;
}

/**
 * A card's pick of «every channel» or one of the page's, kept in `key`. `query` narrows the shared filter to
 * the pick and is spread over the page's channel params.
 */
export function useCardChannel(filters: ReturnType<typeof useChannelsSummaryFilters>, key: string) {
  const [stored, setChannel] = useSyncedWithQueryState<string | null>(
    key,
    (q) => q || null,
    (v) => v,
  );

  const {data: accounts} = useQuery(marketplacesGetAccountsShortOptions());
  const pageChannels = filters.channels;
  const onPage = (channel: string) => pageChannels.length === 0 || pageChannels.includes(channel);

  const options: CardChannelOption[] = [
    {value: null, label: "Все каналы"},
    ...[...(accounts ?? [])]
      .sort((a, b) => a.name.localeCompare(b.name, "ru"))
      .filter((a) => onPage(a.id))
      .map((a) => ({value: a.id, label: a.name})),
    ...(onPage(DIRECT_CHANNEL) ? [{value: DIRECT_CHANNEL, label: "Прямые"}] : []),
  ];

  // A channel the page filter has since dropped falls back to all of them rather than showing nothing
  const channel = stored && onPage(stored) ? stored : null;

  const query =
    channel === DIRECT_CHANNEL
      ? {IncludeMarketplaces: false, MarketplaceAccountIds: undefined, IncludeDirect: true}
      : channel
        ? {IncludeMarketplaces: true, MarketplaceAccountIds: [channel], IncludeDirect: false}
        : {};

  return {
    options,
    channel,
    label: options.find((o) => o.value === channel)?.label ?? "",
    query,
    setChannel,
  };
}
