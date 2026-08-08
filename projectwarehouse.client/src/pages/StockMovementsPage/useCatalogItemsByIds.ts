import {useQueries} from "@tanstack/react-query";
import {catalogGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemSelectDto} from "@/api/types.gen";

/**
 * Resolves ids restored from the URL into DTOs for `CatalogItemsSelect`. The pivot response cannot
 * supply them — an item with no movement in the range gets no column.
 */
export function useCatalogItemsByIds(ids: string[]): Map<string, CatalogItemSelectDto> {
  return useQueries({
    queries: ids.map((id) => ({
      ...catalogGetByIdOptions({path: {id}}),
      staleTime: 5 * 60_000,
      meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
    })),
    combine: (results) =>
      new Map(
        results
          .map((result) => result.data)
          .filter((dto) => dto !== undefined)
          .map((dto) => [
            dto.id,
            {
              id: dto.id,
              type: dto.type,
              name: dto.name,
              fullName: dto.fullName,
              article: dto.article,
              isArchived: dto.isArchived,
            } satisfies CatalogItemSelectDto,
          ]),
      ),
  });
}
