import {useMemo} from "react";
import {useQuery} from "@tanstack/react-query";
import {catalogGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemSelectDto, CatalogItemType} from "@/api/types.gen";
import {useCatalogItemsByIds} from "@/hooks/useCatalogItemsByIds";

interface VariantStep {
  id: string;
  type: CatalogItemType;
}

interface VariationOptionsResult {
  items: CatalogItemSelectDto[];
  isLoading: boolean;
}

/** Members of a variation, resolved into pickable options. */
function useVariationOptions(catalogItemId: string | null): VariationOptionsResult {
  const catalogQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: catalogItemId ?? ""}}),
    enabled: !!catalogItemId,
  });
  const memberIds = useMemo(() => catalogQuery.data?.memberIds ?? [], [catalogQuery.data]);
  const members = useCatalogItemsByIds(memberIds);

  const items = useMemo(
    () => memberIds.map((id) => members.items.get(id)).filter((item) => item !== undefined),
    [memberIds, members.items],
  );

  return {items, isLoading: !!catalogItemId && (catalogQuery.isLoading || members.isLoading)};
}

/** The variant actually taken off the shelf — the last link of the chain. */
function chainLeaf(chain: VariantStep[]): VariantStep | null {
  const last = chain.at(-1);
  return last && last.type !== "variation" ? last : null;
}

export type {VariantStep};
export {chainLeaf, useVariationOptions};
