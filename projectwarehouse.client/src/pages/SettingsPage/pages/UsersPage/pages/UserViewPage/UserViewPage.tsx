import PageLoader from "@/components/PageLoader";
import {useCallback, useState} from "react";
import {Link as RouterLink, useParams} from "react-router";
import {Box, Button, Chip, Paper, Stack, Tooltip} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import {getPermissionLabel} from "@/utils/permissionLabels";
import LockResetIcon from "@mui/icons-material/LockReset";
import DeleteIcon from "@mui/icons-material/Delete";
import {useQuery, useQueryClient} from "@tanstack/react-query";
import {byOperation} from "@/utils/queryKeys";
import {useStaleData} from "@/hooks/useStaleData";
import {usersGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageTitle from "@/components/PageTitle.tsx";
import PageGenericHeader from "@/components/PageGenericHeader";
import ViewableUserAvatar from "@/components/ViewableUserAvatar";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ChangePasswordDialog from "./ChangePasswordDialog";
import DeleteUserDialog from "./DeleteUserDialog";
import InfoRow from "@/components/InfoRow.tsx";
import LoadingOverlay from "@/components/LoadingOverlay";
import {useHasPermission} from "@/hooks/usePermission.ts";

function UserViewPage() {
  const {id} = useParams<{id: string}>();
  const queryClient = useQueryClient();
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const hasDeleteUserPermission = useHasPermission("users.delete");
  const hasChangeUserPasswordPermission = useHasPermission("users.reset_password");
  const hasEditUserPermission = useHasPermission("users.reset_password");

  const {
    data: user,
    isLoading,
    isFetching,
    isError,
    isRefetchError,
    error,
    dataUpdatedAt,
  } = useQuery({
    ...usersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  // Read-only, so there is nothing to lose: the warning never becomes a banner, the data just reloads.
  const refreshUser = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: byOperation("usersGetById", {path: {id: id!}})});
  }, [queryClient, id]);

  const {showLoadingOverlay} = useStaleData("user", id, {
    dataUpdatedAt,
    isFetching,
    isLoading,
    onRefresh: refreshUser,
  });

  if (isLoading) return <PageLoader inline />;

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!user) return <NotFound />;

  return (
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={showLoadingOverlay} alignTop />
      <Stack spacing={2}>
        <PageTitle title={user.username} />
        <AppBreadcrumbs
          path={[
            {name: "Сотрудники", link: "/settings/employees"},
            {name: user.username},
            {name: "Просмотр"},
          ]}
          viewersOf={{entityType: "user", entityId: id}}
        />
        <PageGenericHeader
          title={user.username}
          actions={
            <>
              {hasEditUserPermission && (
                <Button
                  variant="outlined"
                  startIcon={<EditIcon />}
                  component={RouterLink}
                  to={`/settings/employees/${id}/edit`}
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
          <Stack
            direction={{xs: "column", sm: "row"}}
            spacing={3}
            sx={{p: 3, alignItems: {xs: "center", sm: "flex-start"}}}
          >
            <ViewableUserAvatar
              userId={user.id}
              name={user.firstName ?? user.username}
              avatar={user.avatar}
            />
            <Stack spacing={1.5} sx={{flexGrow: 1, minWidth: 0, alignSelf: "stretch"}}>
              <InfoRow label="Email" value={user.email ?? "—"} />
              <InfoRow label="Имя" value={user.firstName ?? "—"} />
              <InfoRow label="Фамилия" value={user.lastName ?? "—"} />
              <InfoRow
                label="Роли"
                value={
                  user.roles.length > 0 ? (
                    <Stack direction="row" sx={{flexWrap: "wrap", gap: 0.5}}>
                      {user.roles.map((role) => (
                        <Chip key={role.id} label={role.name} size="small" />
                      ))}
                    </Stack>
                  ) : (
                    "—"
                  )
                }
              />
              <InfoRow
                label="Прямые права"
                value={
                  user.directPermissions.length > 0 ? (
                    <Stack direction="row" sx={{flexWrap: "wrap", gap: 0.5}}>
                      {user.directPermissions.map((p) => (
                        <Tooltip key={p} title={p} arrow>
                          <Chip label={getPermissionLabel(p)} size="small" />
                        </Tooltip>
                      ))}
                    </Stack>
                  ) : (
                    "—"
                  )
                }
              />
              <InfoRow
                label="Склады"
                value={
                  user.assignedWarehouses.length > 0 ? (
                    <Stack direction="row" sx={{flexWrap: "wrap", gap: 0.5}}>
                      {user.assignedWarehouses.map((w) => (
                        <Chip key={w.id} label={w.name} size="small" />
                      ))}
                    </Stack>
                  ) : (
                    "—"
                  )
                }
              />
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
    </Box>
  );
}

export default UserViewPage;
