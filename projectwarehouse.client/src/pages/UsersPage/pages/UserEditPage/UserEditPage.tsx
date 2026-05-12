import {useEffect, useMemo, useState} from "react";
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
import {type Control, Controller, useController, useForm} from "react-hook-form";
import {useQuery, useMutation, useQueryClient} from "@tanstack/react-query";
import {
  permissionsGetAllOptions,
  rolesSearchOptions,
  usersGetByIdOptions,
  usersGetByIdQueryKey,
  usersUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import type {RoleDto} from "@/api/types.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {useHasPermission} from "@/hooks/usePermission";
import {useDebounce} from "@/hooks/useDebounce";
import {FormTextField} from "@/components/form/FormTextField";
import {isNotFoundError} from "@/utils/errorUtils";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";

type EditFormValues = {
  email: string;
  firstName: string;
  lastName: string;
  roles: RoleDto[];
  directPermissions: string[];
};

function UserEditPage() {
  const {id} = useParams<{id: string}>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const canManageRoles = useHasPermission("users.manage_roles_and_permissions");

  const userQuery = useQuery({
    ...usersGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });
  const permissionsQuery = useQuery(permissionsGetAllOptions());

  const [rolesInput, setRolesInput] = useState("");
  const debouncedRolesInput = useDebounce(rolesInput, 300);
  const rolesSearchQuery = useQuery({
    ...rolesSearchOptions({query: {searchString: debouncedRolesInput || undefined}}),
    enabled: canManageRoles,
  });

  const form = useForm<EditFormValues>({
    defaultValues: {email: "", firstName: "", lastName: "", roles: [], directPermissions: []},
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
      },
      {keepDirtyValues: true},
    );
  }, [userQuery.data, reset]);

  const mutation = useMutation({
    ...usersUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: usersGetByIdQueryKey({path: {id: id!}})});
      navigate(`/users/${id}`);
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

  if (userQuery.isError)
    return isNotFoundError(userQuery.error) ? <NotFound /> : <QueryError error={userQuery.error} />;
  if (!userQuery.data) return <NotFound />;

  const user = userQuery.data;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Пользователи", link: "/users"},
          {name: user.username, link: `/users/${id}`},
          {name: "Редактировать"},
        ]}
      />
      <PageGenericHeader title={`Редактировать: ${user.username}`} />

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

            {canManageRoles && (
              <>
                <RolesAutocomplete
                  control={form.control}
                  rolesInput={rolesInput}
                  setRolesInput={setRolesInput}
                  searchResults={rolesSearchQuery.data ?? []}
                  disabled={mutation.isPending}
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
              <Button onClick={() => navigate(`/users/${id}`)} disabled={mutation.isPending}>
                Отмена
              </Button>
              <Button type="submit" variant="contained" disabled={mutation.isPending}>
                {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Сохранить"}
              </Button>
            </Stack>
          </Stack>
        </Box>
      </Paper>
    </Stack>
  );
}

interface RolesAutocompleteProps {
  control: Control<EditFormValues>;
  rolesInput: string;
  setRolesInput: (v: string) => void;
  searchResults: RoleDto[];
  disabled: boolean;
}

function RolesAutocomplete({
  control,
  rolesInput,
  setRolesInput,
  searchResults,
  disabled,
}: RolesAutocompleteProps) {
  const {field} = useController({control, name: "roles"});

  // merge search results with selected roles so selected items stay visible when query changes
  const options = useMemo(() => {
    const seen = new Set(searchResults.map((r) => r.id));
    return [...searchResults, ...field.value.filter((r) => !seen.has(r.id))];
  }, [searchResults, field.value]);

  return (
    <Autocomplete
      multiple
      options={options}
      value={field.value}
      onChange={(_, v) => field.onChange(v)}
      inputValue={rolesInput}
      onInputChange={(_, v) => setRolesInput(v)}
      getOptionLabel={(r) => r.name}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterSelectedOptions
      filterOptions={(x) => x}
      disabled={disabled}
      renderInput={(params) => <TextField {...params} label="Роли" />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => (
          <Chip label={option.name} {...getItemProps({index})} key={option.id} size="small" />
        ))
      }
    />
  );
}

export default UserEditPage;
