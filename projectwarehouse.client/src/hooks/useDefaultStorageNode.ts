import {useMemo} from "react";
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

  // A fresh object per render would leak into every caller's effect deps and re-fire it endlessly.
  const data = query.data;
  return useMemo(() => (data ? {nodeId: data.id, nodePath: data.name} : null), [data]);
}

export {useDefaultStorageNode};
