import {useMemo} from "react";
import {useInfiniteQuery, useQuery} from "@tanstack/react-query";
import {statisticsGetPivot} from "@/api";
import type {
  StockMovementMetricDto,
  StockMovementPivotRequest,
  StockMovementPivotRowDto,
} from "@/api/types.gen";
import {addDays, todayDateOnly} from "@/utils/dateOnly";

export const WINDOW_DAYS = 30;

/** ~2 years back. Without a stop the list would scroll into empty windows forever. */
const MAX_WINDOWS = 24;

/** `columnLimit` is `[Range(1, 200)]` on the server. */
const MAX_COLUMNS = 200;

export interface StockMovementsFilterValue {
  catalogItemIds: string[];
  from: string | null;
  to: string | null;
  warehouseId: string | null;
  storagePlaceId: string | null;
  nodeId: string | null;
  userId: string | null;
}

function buildBody(
  filter: StockMovementsFilterValue,
  metrics: StockMovementMetricDto[],
  from: string,
  to: string,
): StockMovementPivotRequest {
  return {
    from,
    to,
    warehouseId: filter.warehouseId,
    storagePlaceId: filter.storagePlaceId,
    nodeId: filter.nodeId,
    userId: filter.userId,
    catalogItemIds: filter.catalogItemIds,
    metrics,
    columnLimit: Math.min(Math.max(filter.catalogItemIds.length, 1), MAX_COLUMNS),
  };
}

async function fetchPivot(body: StockMovementPivotRequest, signal: AbortSignal) {
  const response = await statisticsGetPivot({body, signal, throwOnError: true});
  return response.data;
}

/**
 * The pivot endpoint takes a date range, not a page. With no lower bound the range is walked backwards
 * in {@link WINDOW_DAYS}-day windows; with one, a single request covers it.
 *
 * Columns are **not** taken from the response: the server only returns a column for an item that
 * actually moved, so the table would gain and lose columns as windows load. The selection drives them.
 */
export function useStockMovementsPivot(
  filter: StockMovementsFilterValue,
  metrics: StockMovementMetricDto[],
) {
  const enabled = filter.catalogItemIds.length > 0;
  const isInfinite = filter.from === null;
  const anchor = filter.to ?? todayDateOnly();

  // A metric's name is a column label and changes nothing about the figures, so it stays out of the key:
  // keying on it re-POSTs the whole pivot on every keystroke in the editor's name field.
  const predicates = metrics.map(({actions, directions, receiptTagIds}) => ({
    actions,
    directions,
    receiptTagIds,
  }));

  // `name` is `[Required]` server-side, so a half-typed metric would 400 and replace the table with an
  // error. The placeholder keeps the request valid while the user is still naming the column.
  const named = metrics.map((metric, index) => ({
    ...metric,
    name: metric.name.trim() || `#${index + 1}`,
  }));

  const rangeQuery = useQuery({
    queryKey: ["stockMovementsPivot", "range", filter, predicates, anchor],
    queryFn: ({signal}) => fetchPivot(buildBody(filter, named, filter.from!, anchor), signal),
    enabled: enabled && !isInfinite,
  });

  const infiniteQuery = useInfiniteQuery({
    queryKey: ["stockMovementsPivot", "infinite", filter, predicates, anchor],
    initialPageParam: 0,
    queryFn: ({pageParam, signal}) => {
      const to = addDays(anchor, -WINDOW_DAYS * pageParam);
      return fetchPivot(buildBody(filter, named, addDays(to, -(WINDOW_DAYS - 1)), to), signal);
    },
    getNextPageParam: (_last, pages) => (pages.length >= MAX_WINDOWS ? undefined : pages.length),
    enabled: enabled && isInfinite,
  });

  const active = isInfinite ? infiniteQuery : rangeQuery;

  // Newest day first in both modes; the API returns each window oldest first.
  const rows = useMemo<StockMovementPivotRowDto[]>(() => {
    const pages = isInfinite ? infiniteQuery.data?.pages : rangeQuery.data && [rangeQuery.data];
    return (pages ?? []).flatMap((page) => [...page.rows].reverse());
  }, [isInfinite, infiniteQuery.data, rangeQuery.data]);

  // The warehouse's own zone wins over the caller's, so the applied one has to be shown, not assumed.
  const timeZoneId = isInfinite
    ? infiniteQuery.data?.pages[0]?.timeZoneId
    : rangeQuery.data?.timeZoneId;

  return {
    rows,
    timeZoneId,
    isInfinite,
    isLoading: enabled && active.isLoading,
    isFetching: active.isFetching,
    isFetchingNextPage: isInfinite && infiniteQuery.isFetchingNextPage,
    hasNextPage: isInfinite && infiniteQuery.hasNextPage,
    fetchNextPage: infiniteQuery.fetchNextPage,
    error: active.error,
    refetch: active.refetch,
  };
}
