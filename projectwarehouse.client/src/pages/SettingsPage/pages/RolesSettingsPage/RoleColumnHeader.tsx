import {observer} from "mobx-react-lite";
import {IconButton, Stack, Typography} from "@mui/material";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import {useModal} from "@/hooks/useModal";
import type {EditableRole} from "./rolesStore";
import {useRolesStore} from "./RolesStoreContext";
import RenameRoleDialog from "./RenameRoleDialog";

interface RoleColumnHeaderProps {
  role: EditableRole;
  dragHandleProps?: Record<string, unknown>;
}

export default observer(function RoleColumnHeader({role, dragHandleProps}: RoleColumnHeaderProps) {
  const {store, canEdit} = useRolesStore();
  const {showModal, showConfirm} = useModal();

  async function handleRename() {
    const result = await showModal(RenameRoleDialog, {initialName: role.name});
    if (result !== null) {
      store.renameRole(role.tempId, result);
    }
  }

  async function handleDelete() {
    const ok = await showConfirm({
      title: "Удалить роль?",
      message: `Роль "${role.name || "Новая роль"}" будет удалена. Изменение вступит в силу после сохранения.`,
      confirmText: "Удалить",
      severity: "error",
    });
    if (ok) {
      store.removeRole(role.tempId);
    }
  }

  return (
    <Stack direction="row" spacing={0.5} sx={{minWidth: 120, alignItems: "center"}}>
      {canEdit && dragHandleProps && (
        <IconButton
          {...dragHandleProps}
          size="small"
          tabIndex={-1}
          sx={{cursor: "grab", flexShrink: 0, "&:active": {cursor: "grabbing"}}}
        >
          <DragIndicatorIcon fontSize="small" />
        </IconButton>
      )}
      <Typography
        variant="body2"
        sx={{
          fontWeight: 600,
          flexGrow: 1,
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
          fontStyle: role.name ? "normal" : "italic",
          opacity: role.name ? 1 : 0.5,
        }}
      >
        {role.name || "Новая роль"}
      </Typography>
      {canEdit && (
        <>
          <IconButton size="small" onClick={handleRename}>
            <EditIcon fontSize="small" />
          </IconButton>
          <IconButton size="small" color="error" onClick={handleDelete}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </>
      )}
    </Stack>
  );
});
