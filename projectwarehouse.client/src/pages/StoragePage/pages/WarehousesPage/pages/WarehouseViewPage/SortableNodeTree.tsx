import {
  DndContext,
  type DragEndEvent,
  DragOverlay,
  type DragStartEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  arrayMove,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {Box, Chip, IconButton, Stack} from "@mui/material";
import {type NodeOrderItem, type StoragePlaceNodeDto} from "@/api";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import DriveFileRenameOutlineIcon from "@mui/icons-material/DriveFileRenameOutline";
import {useState} from "react";

export interface SortableNodeTreeActions {
  onAddChild: (parentId: string) => void;
  onRename: (node: StoragePlaceNodeDto) => void;
  onDelete: (node: StoragePlaceNodeDto) => void;
  onReorder: (items: NodeOrderItem[]) => void;
  isDisabled: boolean;
}

interface NodeRowProps {
  node: StoragePlaceNodeDto;
  dragHandleProps?: object;
  isDisabled: boolean;
  actions: SortableNodeTreeActions;
}

function NodeRow({node, dragHandleProps, isDisabled, actions}: NodeRowProps) {
  return (
    <Stack
      direction="row"
      spacing={0.5}
      sx={{alignItems: "center", py: 0.25, borderRadius: 1, "&:hover": {bgcolor: "action.hover"}}}
    >
      <Box
        {...dragHandleProps}
        sx={{
          display: "flex",
          alignItems: "center",
          cursor: isDisabled ? "default" : "grab",
          touchAction: "none",
          color: "text.disabled",
          flexShrink: 0,
        }}
      >
        <DragIndicatorIcon fontSize="small" />
      </Box>
      <span style={{flex: 1, fontSize: "0.875rem"}}>{node.name}</span>
      {node.totalItemsCount > 0 && (
        <Chip label={node.totalItemsCount} size="small" color="primary" variant="outlined" />
      )}
      <IconButton
        size="small"
        title="Добавить дочернюю ячейку"
        disabled={isDisabled}
        onClick={() => actions.onAddChild(node.id)}
      >
        <AddIcon fontSize="small" />
      </IconButton>
      <IconButton
        size="small"
        title="Переименовать"
        disabled={isDisabled}
        onClick={() => actions.onRename(node)}
      >
        <DriveFileRenameOutlineIcon fontSize="small" />
      </IconButton>
      <IconButton
        size="small"
        color="error"
        title="Удалить"
        disabled={isDisabled}
        onClick={() => actions.onDelete(node)}
      >
        <DeleteIcon fontSize="small" />
      </IconButton>
    </Stack>
  );
}

interface SortableNodeItemProps {
  node: StoragePlaceNodeDto;
  allNodes: StoragePlaceNodeDto[];
  depth: number;
  actions: SortableNodeTreeActions;
}

function SortableNodeItem({node, allNodes, depth, actions}: SortableNodeItemProps) {
  const {attributes, listeners, setNodeRef, transform, transition, isDragging} = useSortable({
    id: node.id,
  });

  const children = allNodes
    .filter((n) => n.parentNodeId === node.id)
    .sort((a, b) => a.order - b.order || a.name.localeCompare(b.name));

  return (
    <Box
      ref={setNodeRef}
      sx={{
        transform: isDragging ? undefined : CSS.Transform.toString(transform),
        transition: isDragging ? undefined : transition,
        opacity: isDragging ? 0 : 1,
        pl: `${depth * 20}px`,
      }}
    >
      <NodeRow
        node={node}
        dragHandleProps={actions.isDisabled ? undefined : {...attributes, ...listeners}}
        isDisabled={actions.isDisabled}
        actions={actions}
      />
      {children.length > 0 && (
        <SortableGroup nodes={children} allNodes={allNodes} depth={depth + 1} actions={actions} />
      )}
    </Box>
  );
}

interface SortableGroupProps {
  nodes: StoragePlaceNodeDto[];
  allNodes: StoragePlaceNodeDto[];
  depth: number;
  actions: SortableNodeTreeActions;
}

function SortableGroup({nodes, allNodes, depth, actions}: SortableGroupProps) {
  return (
    <SortableContext items={nodes.map((n) => n.id)} strategy={verticalListSortingStrategy}>
      {nodes.map((node) => (
        <SortableNodeItem
          key={node.id}
          node={node}
          allNodes={allNodes}
          depth={depth}
          actions={actions}
        />
      ))}
    </SortableContext>
  );
}

interface SortableNodeTreeProps {
  nodes: StoragePlaceNodeDto[];
  actions: SortableNodeTreeActions;
}

export function SortableNodeTree({nodes, actions}: SortableNodeTreeProps) {
  const [activeId, setActiveId] = useState<string | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, {activationConstraint: {distance: 5}}),
    useSensor(KeyboardSensor, {coordinateGetter: sortableKeyboardCoordinates}),
  );

  const roots = nodes
    .filter((n) => !n.parentNodeId)
    .sort((a, b) => a.order - b.order || a.name.localeCompare(b.name));

  const activeNode = activeId ? nodes.find((n) => n.id === activeId) : null;

  const handleDragStart = ({active}: DragStartEvent) => {
    setActiveId(active.id as string);
  };

  const handleDragEnd = ({active, over}: DragEndEvent) => {
    setActiveId(null);
    if (!over || active.id === over.id || actions.isDisabled) return;

    const activeNode = nodes.find((n) => n.id === active.id);
    const overNode = nodes.find((n) => n.id === over.id);
    if (!activeNode || !overNode) return;
    if (activeNode.parentNodeId !== overNode.parentNodeId) return;

    const siblings = nodes
      .filter((n) => n.parentNodeId === activeNode.parentNodeId)
      .sort((a, b) => a.order - b.order || a.name.localeCompare(b.name));

    const oldIndex = siblings.findIndex((n) => n.id === active.id);
    const newIndex = siblings.findIndex((n) => n.id === over.id);
    const reordered = arrayMove(siblings, oldIndex, newIndex);

    actions.onReorder(reordered.map((n, i) => ({nodeId: n.id, order: i})));
  };

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setActiveId(null)}
    >
      <SortableGroup nodes={roots} allNodes={nodes} depth={0} actions={actions} />
      <DragOverlay dropAnimation={null}>
        {activeNode && (
          <Box
            sx={{
              bgcolor: "background.paper",
              border: 1,
              borderColor: "primary.main",
              borderRadius: 1,
              px: 1,
              py: 0.25,
              boxShadow: 3,
            }}
          >
            <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
              <DragIndicatorIcon fontSize="small" sx={{color: "text.disabled"}} />
              <span style={{fontSize: "0.875rem"}}>{activeNode.name}</span>
            </Stack>
          </Box>
        )}
      </DragOverlay>
    </DndContext>
  );
}
