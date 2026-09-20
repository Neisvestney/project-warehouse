import PageLoader from "@/components/PageLoader";
import {useState} from "react";
import {Box, Button, Chip, Paper, Stack, Tooltip, Typography} from "@mui/material";
import {getPermissionLabel} from "@/utils/permissionLabels";
import LockResetIcon from "@mui/icons-material/LockReset";
import {useQuery} from "@tanstack/react-query";
import {authMeOptions, usersGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import ChangePasswordDialog from "./ChangePasswordDialog";
import ViewableUserAvatar from "@/components/ViewableUserAvatar";
import InfoRow from "@/components/InfoRow.tsx";
import LoadingOverlay from "@/components/LoadingOverlay";
import {useHasPermission} from "@/hooks/usePermission.ts";
import {Link as RouterLink} from "react-router";
import EditIcon from "@mui/icons-material/Edit";

function MyProfilePage() {
  const canEdit = useHasPermission("users.edit_profile");

  const [changePasswordOpen, setChangePasswordOpen] = useState(false);

  const {
    data: user,
    isLoading,
    isFetching,
    isError,
    isRefetchError,
    error,
  } = useQuery({
    ...authMeOptions(),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const {data: userDetails} = useQuery({
    ...usersGetByIdOptions({path: {id: user?.id ?? ""}}),
    enabled: !!user?.id,
  });

  if (isLoading) return <PageLoader inline />;

  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!user) return <NotFound />;

  return (
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={isFetching && !isLoading} alignTop />
      <Stack spacing={2}>
        <AppBreadcrumbs path={[{name: "Мой профиль"}]} />
        <PageGenericHeader
          title={user.username}
          actions={
            <>
              {canEdit && (
                <Button
                  startIcon={<EditIcon />}
                  component={RouterLink}
                  to={`/settings/employees/${user.id}/edit`}
                  variant="outlined"
                >
                  Редактировать профиль
                </Button>
              )}
              <Button
                variant="outlined"
                startIcon={<LockResetIcon />}
                onClick={() => setChangePasswordOpen(true)}
              >
                Сменить пароль
              </Button>
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
              avatar={userDetails?.avatar}
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
                      {user.roles.map((role, i) => (
                        <Chip key={i} label={role} size="small" />
                      ))}
                    </Stack>
                  ) : (
                    "—"
                  )
                }
              />
              <InfoRow
                label="Права"
                value={
                  user.permissions.length > 0 ? (
                    <Stack direction="row" sx={{flexWrap: "wrap", gap: 0.5}}>
                      {user.permissions.map((p, i) => (
                        <Tooltip key={i} title={p} arrow>
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
                  !userDetails ? (
                    <Typography component="span" color="text.secondary">
                      ...
                    </Typography>
                  ) : userDetails.assignedWarehouses.length > 0 ? (
                    <Stack direction="row" sx={{flexWrap: "wrap", gap: 0.5}}>
                      {userDetails.assignedWarehouses.map((w) => (
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
          onClose={() => setChangePasswordOpen(false)}
        />
      </Stack>
    </Box>
  );
}

export default MyProfilePage;
