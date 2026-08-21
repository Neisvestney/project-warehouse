import {useCallback, useEffect} from "react";
import {useNavigate, useParams} from "react-router";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import {getPermissionLabel} from "@/utils/permissionLabels";
import {Controller, useForm} from "react-hook-form";
import {useQuery, useMutation, useQueryClient} from "@tanstack/react-query";
import {
  permissionsGetAllOptions,
  usersGetByIdOptions,
  usersGetByIdQueryKey,
  usersUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import type {RoleDto, WarehouseSummaryDto} from "@/api/types.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {useHasPermission} from "@/hooks/usePermission";
import {FormTextField} from "@/components/form/FormTextField";
import {isNotFoundError} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import {useEditLock} from "@/hooks/useEditLock";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import RolesSelect from "@/components/RolesSelect";
import WarehousesSelect from "@/components/WarehousesSelect";
import LoadingOverlay from "@/components/LoadingOverlay";

type EditFormValues = {
  email: string;
  firstName: string;
  lastName: string;
  roles: RoleDto[];
  directPermissions: string[];
  assignedWarehouses: WarehouseSummaryDto[];
};

function UserEditPage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const canManageRoles = useHasPermission("users.manage_roles_and_permissions");
  const canManageAssignedWarehouses = useHasPermission("users.manage_assigned_warehouses");

  const userQuery = useQuery({
    ...usersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });
  const permissionsQuery = useQuery(permissionsGetAllOptions());

  const form = useForm<EditFormValues>({
    defaultValues: {
      email: "",
      firstName: "",
      lastName: "",
      roles: [],
      directPermissions: [],
      assignedWarehouses: [],
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const {reset} = form;

  useEffect(() => {
    if (!userQuery.data) return;
    reset(
      {
        email: userQuery.data.email ?? "",
        firstName: userQuery.data.firstName ?? "",
        lastName: userQuery.data.lastName ?? "",
        roles: userQuery.data.roles,
        directPermissions: userQuery.data.directPermissions,
        assignedWarehouses: userQuery.data.assignedWarehouses,
      },
      {keepDirtyValues: true},
    );
  }, [userQuery.data, reset]);

  const refreshUser = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: byOperation("usersGetById", {path: {id: id!}})});
  }, [queryClient, id]);

  // A refetch keeps dirty fields (`keepDirtyValues` above), so an untouched form can be refreshed
  // silently and only a modified one needs the banner.
  const lock = useEditLock("user", id, {
    isDirty: form.formState.isDirty,
    dataUpdatedAt: userQuery.dataUpdatedAt,
    onRefresh: refreshUser,
  });

  const mutation = useMutation({
    ...usersUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: usersGetByIdQueryKey({path: {id: id!}})});
      navigate(`/settings/employees/${id}`);
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: id!},
      body: {
        email: values.email || null,
        firstName: values.firstName || null,
        lastName: values.lastName || null,
        roleIds: canManageRoles
          ? values.roles.map((r) => r.id)
          : (userQuery.data?.roles.map((r) => r.id) ?? []),
        directPermissions: canManageRoles
          ? values.directPermissions
          : (userQuery.data?.directPermissions ?? []),
        assignedWarehouseIds: canManageAssignedWarehouses
          ? values.assignedWarehouses.map((w) => w.id)
          : (userQuery.data?.assignedWarehouses.map((w) => w.id) ?? []),
      },
    });
  });

  if (userQuery.isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 8}}>
        <CircularProgress />
      </Box>
    );
  }

  if (userQuery.isError && !userQuery.isRefetchError)
    return isNotFoundError(userQuery.error) ? <NotFound /> : <QueryError error={userQuery.error} />;
  if (!userQuery.data) return <NotFound />;

  const user = userQuery.data;

  return (
    <Box sx={{position: "relative"}}>
      <LoadingOverlay
        open={userQuery.isFetching && !userQuery.isLoading && !form.formState.isDirty}
      />
      <Stack spacing={2}>
        <AppBreadcrumbs
          path={[
            {name: "Сотрудники", link: "/settings/employees"},
            {name: user.username, link: `/settings/employees/${id}`},
            {name: "Редактировать"},
          ]}
          viewersOf={{entityType: "user", entityId: id}}
        />
        <PageGenericHeader title={`Редактировать: ${user.username}`} />

        <EditLockBanner heldBy={lock.heldBy} />
        <StaleDataBanner
          isStale={!lock.heldBy && lock.isStale}
          staleBy={lock.staleBy}
          onRefresh={lock.refresh}
          onDismiss={lock.dismissStale}
        />

        <Paper>
          <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
            <Stack spacing={2.5}>
              <FormTextField
                control={form.control}
                name="email"
                label="Email"
                type="email"
                autoComplete="email"
                disabled={mutation.isPending}
                fullWidth
              />
              <FormTextField
                control={form.control}
                name="firstName"
                label="Имя"
                autoComplete="given-name"
                disabled={mutation.isPending}
                fullWidth
              />
              <FormTextField
                control={form.control}
                name="lastName"
                label="Фамилия"
                autoComplete="family-name"
                disabled={mutation.isPending}
                fullWidth
              />

              {canManageAssignedWarehouses && (
                <Controller
                  control={form.control}
                  name="assignedWarehouses"
                  render={({field}) => (
                    <WarehousesSelect
                      multiple
                      value={field.value}
                      onChange={field.onChange}
                      disabled={mutation.isPending}
                    />
                  )}
                />
              )}

              {canManageRoles && (
                <>
                  <Controller
                    control={form.control}
                    name="roles"
                    render={({field}) => (
                      <RolesSelect
                        multiple
                        value={field.value}
                        onChange={field.onChange}
                        disabled={mutation.isPending}
                      />
                    )}
                  />
                  <Controller
                    control={form.control}
                    name="directPermissions"
                    render={({field}) => (
                      <Autocomplete
                        multiple
                        options={permissionsQuery.data ?? []}
                        value={field.value}
                        onChange={(_, v) => field.onChange(v)}
                        loading={permissionsQuery.isLoading}
                        filterSelectedOptions
                        disabled={mutation.isPending}
                        getOptionLabel={getPermissionLabel}
                        renderOption={(props, option) => (
                          <li {...props} key={option}>
                            <Box>
                              <Typography variant="body2">{getPermissionLabel(option)}</Typography>
                              <Typography variant="caption" color="text.secondary">
                                {option}
                              </Typography>
                            </Box>
                          </li>
                        )}
                        renderInput={(params) => <TextField {...params} label="Прямые права" />}
                        renderValue={(tagValue, getItemProps) =>
                          tagValue.map((option, index) => (
                            <Tooltip key={option} title={option} arrow>
                              <Chip
                                label={getPermissionLabel(option)}
                                {...getItemProps({index})}
                                size="small"
                              />
                            </Tooltip>
                          ))
                        }
                      />
                    )}
                  />
                </>
              )}

              {form.formState.errors.root && (
                <Alert severity="error">{form.formState.errors.root.message}</Alert>
              )}

              <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
                <Button
                  onClick={() => navigate(`/settings/employees/${id}`)}
                  disabled={mutation.isPending}
                >
                  Отмена
                </Button>
                <Button type="submit" variant="contained" disabled={mutation.isPending}>
                  {mutation.isPending ? (
                    <CircularProgress size={22} color="inherit" />
                  ) : (
                    "Сохранить"
                  )}
                </Button>
              </Stack>
            </Stack>
          </Box>
        </Paper>
      </Stack>
    </Box>
  );
}

export default UserEditPage;
