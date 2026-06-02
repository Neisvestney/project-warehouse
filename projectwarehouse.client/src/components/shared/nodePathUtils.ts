import type {StoragePlaceNodeDto} from "@/api/types.gen";

export interface SelectedNode {
  nodeId: string;
  nodePath: string[];
}

export function formatStoragePlaceNodeName(path: string[]): string {
  return path.join(" / ");
}

export function buildNodePath(
  nodes: StoragePlaceNodeDto[],
  nodeId: string,
  storagePlaceName: string,
): string[] {
  const nodeMap = new Map(nodes.map((n) => [n.id, n]));
  const path: string[] = [];
  let current = nodeMap.get(nodeId);
  while (current) {
    path.unshift(current.name);
    current = current.parentNodeId ? nodeMap.get(current.parentNodeId) : undefined;
  }
  return [storagePlaceName, ...path];
}
