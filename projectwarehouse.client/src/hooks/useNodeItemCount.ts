import {useQuery} from "@tanstack/react-query";
import {storagePlacesGetNodeItemCountOptions} from "@/api/@tanstack/react-query.gen";

/**
 * How many pieces of one catalog item lie in one storage node — `null` until it is known.
 * Addresses the node without its storage place, so a default cell or a scanned label works too.
 */
export function useNodeItemCount(
  nodeId: string | null | undefined,
  catalogItemId: string | null | undefined,
): number | null {
  const query = useQuery({
    ...storagePlacesGetNodeItemCountOptions({
      path: {nodeId: nodeId ?? ""},
      query: {catalogItemId: catalogItemId ?? undefined},
    }),
    enabled: !!nodeId && !!catalogItemId,
    meta: {suppressGlobalError: true},
  });

  return query.data?.count ?? null;
}
