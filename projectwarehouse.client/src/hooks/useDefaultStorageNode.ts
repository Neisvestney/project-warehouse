import {useMemo} from "react";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetDefaultNodeOptions} from "@/api/@tanstack/react-query.gen";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

/**
 * Fetches the warehouse's default storage cell (if assigned), for pre-filling pickers.
 *
 * The endpoint answers 404 when no default is assigned, so a null `node` and `isError` are different states:
 * only the first is worth telling the user to go and assign one. Callers that merely pre-fill a picker can
 * ignore the difference and take `useDefaultStorageNode` instead.
 */
function useDefaultStorageNodeQuery(warehouseId: string, enabled = true) {
  const query = useQuery({
    ...warehousesGetDefaultNodeOptions({path: {id: warehouseId}}),
    enabled,
    meta: {suppressGlobalError: true},
    retry: false,
  });

  // A fresh object per render would leak into every caller's effect deps and re-fire it endlessly.
  const data = query.data;
  const node = useMemo(() => (data ? {nodeId: data.id, nodePath: data.name} : null), [data]);

  return {node, isPending: query.isPending, isError: query.isError, error: query.error};
}

function useDefaultStorageNode(warehouseId: string, enabled = true): SelectedNode | null {
  return useDefaultStorageNodeQuery(warehouseId, enabled).node;
}

export {useDefaultStorageNode, useDefaultStorageNodeQuery};
