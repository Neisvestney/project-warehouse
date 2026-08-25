import {observer} from "mobx-react-lite";
import {
  Checkbox,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {
  closestCenter,
  DndContext,
  type DragEndEvent,
  PointerSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {horizontalListSortingStrategy, SortableContext, useSortable} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {getPermissionLabel} from "@/utils/permissionLabels";
import type {EditableRole} from "./rolesStore";
import {useRolesStore} from "./RolesStoreContext";
import RoleColumnHeader from "./RoleColumnHeader";

const SortableRoleHeaderCell = observer(function SortableRoleHeaderCell({
  role,
}: {
  role: EditableRole;
}) {
  "use no memo";

  const {canEdit} = useRolesStore();
  const {attributes, listeners, setNodeRef, transform, transition, isDragging} = useSortable({
    id: role.tempId,
    disabled: !canEdit,
  });

  return (
    <TableCell
      ref={setNodeRef}
      style={{transform: CSS.Transform.toString(transform), transition}}
      sx={{
        position: "sticky",
        top: 0,
        zIndex: isDragging ? 4 : 2,
        bgcolor: "background.paper",
        opacity: isDragging ? 0.5 : 1,
        minWidth: 180,
        verticalAlign: "middle",
      }}
    >
      <RoleColumnHeader role={role} dragHandleProps={{...listeners, ...attributes}} />
    </TableCell>
  );
});

export default observer(function RolesTable({isLoading}: {isLoading: boolean}) {
  "use no memo";

  const {store, canEdit} = useRolesStore();

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {distance: 8},
    }),
  );

  function handleDragEnd(event: DragEndEvent) {
    const {active, over} = event;
    if (over && active.id !== over.id) {
      store.reorderRoles(String(active.id), String(over.id));
    }
  }

  if (isLoading) {
    return (
      <Stack sx={{alignItems: "center", justifyContent: "center", py: 6}}>
        <CircularProgress />
      </Stack>
    );
  }

  if (store.roles.length === 0 && !canEdit) {
    return (
      <Typography color="text.secondary" sx={{py: 4, textAlign: "center"}}>
        Нет ролей
      </Typography>
    );
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext
        items={store.roles.map((r) => r.tempId)}
        strategy={horizontalListSortingStrategy}
      >
        <TableContainer component={Paper} sx={{overflow: "auto", maxHeight: "calc(100vh - 220px)"}}>
          <Table stickyHeader size="small">
            <TableHead>
              <TableRow>
                <TableCell
                  sx={{
                    position: "sticky",
                    top: 0,
                    left: 0,
                    zIndex: 3,
                    bgcolor: "background.paper",
                    minWidth: 240,
                  }}
                />
                {store.roles.map((role) => (
                  <SortableRoleHeaderCell key={role.tempId} role={role} />
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {store.allPermissions.map((permission) => (
                <TableRow key={permission}>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 1,
                      bgcolor: "background.paper",
                    }}
                  >
                    {getPermissionLabel(permission)}
                  </TableCell>
                  {store.roles.map((role) => (
                    <TableCell key={role.tempId} align="center">
                      <Checkbox
                        checked={role.hasPermission(permission)}
                        onChange={() => store.togglePermission(role.tempId, permission)}
                        disabled={!canEdit}
                        size="small"
                      />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </SortableContext>
    </DndContext>
  );
});
