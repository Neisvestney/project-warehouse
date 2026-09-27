import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsSummaryOptions} from "@/api/@tanstack/react-query.gen";
import type {Period} from "../period/periodSelection";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

/** The summary over a card's own period; while it matches the page's, the page's query is reused. */
export function useCardSummary(
  filters: ReturnType<typeof useChannelsSummaryFilters>,
  period: Period,
) {
  return useQuery({
    ...analyticsGetChannelsSummaryOptions({
      query: {...filters.query, From: period.from, To: period.to},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });
}
