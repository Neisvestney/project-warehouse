import {Box, Chip, CircularProgress, Stack, Typography} from "@mui/material";
import {SimpleTreeView} from "@mui/x-tree-view/SimpleTreeView";
import {TreeItem} from "@mui/x-tree-view/TreeItem";
import {useMemo} from "react";

export interface StoragePlaceNodeTreeNode {
  id: string;
  name: string;
  parentNodeId?: string | null;
  order: number;
  totalItemsCount: number;
  hasOrderItems?: boolean;
}

export interface StoragePlaceNodeTreeProps {
  nodes: StoragePlaceNodeTreeNode[];
  selectedNodeId?: string | null;
  onSelect?: (id: string) => void;
  isLoading?: boolean;
  expandedItems?: string[];
  onExpandedItemsChange?: (ids: string[]) => void;
}

type TreeNode = StoragePlaceNodeTreeNode & {
  children: TreeNode[];
  childrenItemsTotalCount: number;
};

function buildTree(nodes: StoragePlaceNodeTreeNode[]): TreeNode[] {
  const nodeMap = new Map<string, TreeNode>();

  for (const node of nodes) {
    nodeMap.set(node.id, {...node, children: [], childrenItemsTotalCount: 0});
  }

  const roots: TreeNode[] = [];
  for (const treeNode of nodeMap.values()) {
    if (treeNode.parentNodeId) {
      nodeMap.get(treeNode.parentNodeId)?.children.push(treeNode);
    } else {
      roots.push(treeNode);
    }
  }

  const calcChildrenTotal = (node: TreeNode): number => {
    const total = node.children.reduce(
      (sum, child) => sum + child.totalItemsCount + calcChildrenTotal(child),
      0,
    );
    node.childrenItemsTotalCount = total;
    return total;
  };

  const sortNodes = (list: TreeNode[]) => {
    list.sort((a, b) => a.order - b.order || a.name.localeCompare(b.name));
    for (const node of list) sortNodes(node.children);
  };

  for (const root of roots) calcChildrenTotal(root);
  sortNodes(roots);

  return roots;
}

function renderNodes(nodes: TreeNode[]): React.ReactNode {
  return nodes.map((node) => (
    <TreeItem
      key={node.id}
      itemId={node.id}
      label={
        <Stack direction="row" spacing={1} sx={{alignItems: "center", py: 0.25}}>
          {node.hasOrderItems !== undefined && (
            <Box
              component="span"
              sx={{
                width: 8,
                height: 8,
                borderRadius: "50%",
                bgcolor: node.hasOrderItems ? "success.main" : "grey.400",
                display: "inline-block",
                flexShrink: 0,
              }}
            />
          )}
          <span>{node.name}</span>
          {node.children.length > 0 && node.childrenItemsTotalCount > 0 && (
            <Chip
              label={node.childrenItemsTotalCount}
              size="small"
              color="primary"
              variant="outlined"
            />
          )}
          {node.totalItemsCount > 0 && (
            <Chip label={node.totalItemsCount} size="small" color="primary" variant="outlined" />
          )}
        </Stack>
      }
    >
      {renderNodes(node.children)}
    </TreeItem>
  ));
}

function StoragePlaceNodeTree({
  nodes,
  selectedNodeId,
  onSelect,
  isLoading,
  expandedItems,
  onExpandedItemsChange,
}: StoragePlaceNodeTreeProps) {
  const tree = useMemo(() => buildTree(nodes), [nodes]);

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 3}}>
        <CircularProgress size={32} />
      </Box>
    );
  }

  if (nodes.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        Ячейки не найдены
      </Typography>
    );
  }

  return (
    <SimpleTreeView
      selectedItems={selectedNodeId ?? null}
      onSelectedItemsChange={(_e, nodeId) => {
        if (nodeId && typeof nodeId === "string") onSelect?.(nodeId);
      }}
      expandedItems={expandedItems}
      onExpandedItemsChange={
        onExpandedItemsChange ? (_e, ids) => onExpandedItemsChange(ids) : undefined
      }
    >
      {renderNodes(tree)}
    </SimpleTreeView>
  );
}

export default StoragePlaceNodeTree;
