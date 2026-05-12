import {useState} from "react";
import {Link as RouterLink, useParams} from "react-router";
import {Box, Button, Chip, CircularProgress, Paper, Stack, Typography} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import LockResetIcon from "@mui/icons-material/LockReset";
import DeleteIcon from "@mui/icons-material/Delete";
import {useQuery} from "@tanstack/react-query";
import {usersGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import ChangePasswordDialog from "./ChangePasswordDialog";
import DeleteUserDialog from "./DeleteUserDialog";

function UserViewPage() {
  const {id} = useParams<{id: string}>();
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const {data: user, isLoading} = useQuery(usersGetByIdOptions({path: {id: id!}}));

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (!user) return null;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Пользователи", link: "/users"}, {name: user.username}, {name: "Просмотр"}]}
      />
      <PageGenericHeader
        title={user.username}
        right={
          <>
            <Button
              variant="outlined"
              startIcon={<EditIcon />}
              component={RouterLink}
              to={`/users/${id}/edit`}
            >
              Редактировать
            </Button>
            <Button
              variant="outlined"
              startIcon={<LockResetIcon />}
              onClick={() => setChangePasswordOpen(true)}
            >
              Сменить пароль
            </Button>
            <Button
              variant="outlined"
              color="error"
              startIcon={<DeleteIcon />}
              onClick={() => setDeleteOpen(true)}
            >
              Удалить
            </Button>
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
              Права
            </Typography>
            <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
              {user.directPermissions.length > 0 ? (
                user.directPermissions.map((p) => <Chip key={p} label={p} size="small" />)
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

function InfoRow({label, value}: {label: string; value: string}) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
      <Typography color="text.secondary" sx={{width: 160, flexShrink: 0}}>
        {label}
      </Typography>
      <Typography>{value}</Typography>
    </Stack>
  );
}

export default UserViewPage;
