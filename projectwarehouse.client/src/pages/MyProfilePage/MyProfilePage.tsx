import {useState} from "react";
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
import InfoRow from "@/components/InfoRow.tsx";
import LoadingOverlay from "@/components/LoadingOverlay";

function MyProfilePage() {
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
    <Box sx={{position: "relative"}}>
      <LoadingOverlay open={isFetching && !isLoading} />
      <Stack spacing={2}>
        <AppBreadcrumbs path={[{name: "Мой профиль"}]} />
        <PageGenericHeader
          title={user.username}
          right={
            <>
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
                  user.roles.map((role, i) => <Chip key={i} label={role} size="small" />)
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
                {user.permissions.length > 0 ? (
                  user.permissions.map((p, i) => (
                    <Tooltip key={i} title={p} arrow>
                      <Chip label={getPermissionLabel(p)} size="small" />
                    </Tooltip>
                  ))
                ) : (
                  <Typography>—</Typography>
                )}
              </Stack>
            </Stack>
            <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
              <Typography color="text.secondary" sx={{width: 160, flexShrink: 0, pt: 0.25}}>
                Склады
              </Typography>
              <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
                {userDetails ? (
                  userDetails.assignedWarehouses.length > 0 ? (
                    userDetails.assignedWarehouses.map((w) => (
                      <Chip key={w.id} label={w.name} size="small" />
                    ))
                  ) : (
                    <Typography>—</Typography>
                  )
                ) : (
                  <Typography color="text.secondary">...</Typography>
                )}
              </Stack>
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
