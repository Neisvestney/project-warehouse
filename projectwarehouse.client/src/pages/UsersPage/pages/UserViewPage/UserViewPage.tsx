import {useState} from "react";
import {Link as RouterLink, useParams} from "react-router";
import {
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import {getPermissionLabel} from "@/utils/permissionLabels";
import LockResetIcon from "@mui/icons-material/LockReset";
import DeleteIcon from "@mui/icons-material/Delete";
import {useQuery} from "@tanstack/react-query";
import {usersGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ChangePasswordDialog from "./ChangePasswordDialog";
import DeleteUserDialog from "./DeleteUserDialog";
import InfoRow from "@/components/InfoRow.tsx";
import {useHasPermission} from "@/hooks/usePermission.ts";

function UserViewPage() {
  const {id} = useParams<{id: string}>();
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const hasDeleteUserPermission = useHasPermission("users.delete");
  const hasChangeUserPasswordPermission = useHasPermission("users.reset_password");
  const hasEditUserPermission = useHasPermission("users.reset_password");

  const {
    data: user,
    isLoading,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...usersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!user) return <NotFound />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Пользователи", link: "/users"}, {name: user.username}, {name: "Просмотр"}]}
      />
      <PageGenericHeader
        title={user.username}
        right={
          <>
            {hasEditUserPermission && (
              <Button
                variant="outlined"
                startIcon={<EditIcon />}
                component={RouterLink}
                to={`/users/${id}/edit`}
              >
                Редактировать
              </Button>
            )}
            {hasChangeUserPasswordPermission && (
              <Button
                variant="outlined"
                startIcon={<LockResetIcon />}
                onClick={() => setChangePasswordOpen(true)}
              >
                Сменить пароль
              </Button>
            )}
            {hasDeleteUserPermission && (
              <Button
                variant="outlined"
                color="error"
                startIcon={<DeleteIcon />}
                onClick={() => setDeleteOpen(true)}
              >
                Удалить
              </Button>
            )}
          </>
        }
      />

      <Paper>
        <Stack spacing={1.5} sx={{p: 3}}>
          <InfoRow label="Email" value={user.email ?? "—"} />
          <InfoRow label="Имя" value={user.firstName ?? "—"} />
          <InfoRow label="Фамилия" value={user.lastName ?? "—"} />
          <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
            <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
              Роли
            </Typography>
            <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
              {user.roles.length > 0 ? (
                user.roles.map((role) => <Chip key={role.id} label={role.name} size="small" />)
              ) : (
                <Typography>—</Typography>
              )}
            </Stack>
          </Stack>
          <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
            <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
              Прямые
            </Typography>
            <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
              {user.directPermissions.length > 0 ? (
                user.directPermissions.map((p) => (
                  <Tooltip key={p} title={p} arrow>
                    <Chip label={getPermissionLabel(p)} size="small" />
                  </Tooltip>
                ))
              ) : (
                <Typography>—</Typography>
              )}
            </Stack>
          </Stack>
        </Stack>
      </Paper>

      <ChangePasswordDialog
        open={changePasswordOpen}
        userId={id!}
        onClose={() => setChangePasswordOpen(false)}
      />
      <DeleteUserDialog
        open={deleteOpen}
        userId={id!}
        username={user.username}
        onClose={() => setDeleteOpen(false)}
      />
    </Stack>
  );
}

export default UserViewPage;
