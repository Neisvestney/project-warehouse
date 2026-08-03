import {useQuery} from "@tanstack/react-query";
import {warehousesGetDefaultNodeOptions} from "@/api/@tanstack/react-query.gen";
import type {SelectedNode} from "@/components/shared/nodePathUtils";

/** Fetches the warehouse's default storage cell (if assigned), for pre-filling pickers. */
function useDefaultStorageNode(warehouseId: string, enabled = true): SelectedNode | null {
  const query = useQuery({
    ...warehousesGetDefaultNodeOptions({path: {id: warehouseId}}),
    enabled,
    meta: {suppressGlobalError: true},
    retry: false,
  });

  if (!query.data) return null;
  return {nodeId: query.data.id, nodePath: query.data.name};
}

export {useDefaultStorageNode};
