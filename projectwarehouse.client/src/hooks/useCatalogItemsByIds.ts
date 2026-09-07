import {useQueries} from "@tanstack/react-query";
import {catalogGetForSelectByIds} from "@/api";
import type {CatalogItemSelectDto} from "@/api/types.gen";

/** Mirrors `CatalogController.MaxSelectIds` — a longer selection travels as several requests. */
const MAX_IDS = 500;

export interface CatalogItemsByIdsResult {
  /** Keyed by id. An id the server does not know is simply absent. */
  items: Map<string, CatalogItemSelectDto>;
  isLoading: boolean;
  isError: boolean;
}

function chunkIds(ids: string[]): string[][] {
  // Sorted so the same set in a different order hits the same cache entry.
  const sorted = [...new Set(ids)].sort();
  const chunks: string[][] = [];
  for (let i = 0; i < sorted.length; i += MAX_IDS) {
    chunks.push(sorted.slice(i, i + MAX_IDS));
  }
  return chunks;
}

/**
 * Resolves ids into `CatalogItemSelectDto`s in one request. Use it wherever a set of ids has to be
 * shown as items — a selection restored from a URL, the members of a variation — instead of fanning
 * out into one `catalogGetById` per id.
 */
export function useCatalogItemsByIds(ids: string[]): CatalogItemsByIdsResult {
  return useQueries({
    queries: chunkIds(ids).map((chunk) => ({
      queryKey: ["catalogItemsByIds", chunk],
      queryFn: async ({signal}: {signal: AbortSignal}) => {
        const response = await catalogGetForSelectByIds({
          body: {ids: chunk},
          signal,
          throwOnError: true,
        });
        return response.data;
      },
      staleTime: 1000,
    })),
    combine: (results) => ({
      items: new Map(results.flatMap((result) => result.data ?? []).map((dto) => [dto.id, dto])),
      isLoading: results.some((result) => result.isLoading),
      isError: results.some((result) => result.isError),
    }),
  });
}
