import {useEffect, useRef, useState} from "react";
import {observer} from "mobx-react-lite";
import {Button, CircularProgress, Stack} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  permissionsGetAllOptions,
  rolesGetAllOptions,
  rolesGetAllQueryKey,
  rolesUpdateAllMutation,
} from "@/api/@tanstack/react-query.gen";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import {useHasPermission} from "@/hooks/usePermission";
import {RolesStore} from "./rolesStore";
import {RolesStoreProvider} from "./RolesStoreContext.tsx";
import RolesTable from "./RolesTable.tsx";
import QueryError from "@/components/QueryError.tsx";

export default observer(function RolesSettingsPage() {
  const [store] = useState(() => new RolesStore());
  const canEdit = useHasPermission("roles.edit");
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  const {
    data: rolesData,
    isLoading: rolesLoading,
    error: rolesError,
    isError: rolesIsError,
    isRefetchError: rolesIsRefetchError,
  } = useQuery({
    ...rolesGetAllOptions(),
    meta: {suppressGlobalError: true},
  });
  const {
    data: permissionsData,
    isLoading: permissionsLoading,
    error: permissionsError,
    isError: permissionsIsError,
    isRefetchError: permissionsIsRefetchError,
  } = useQuery({
    ...permissionsGetAllOptions(),
    meta: {suppressGlobalError: true},
  });

  const isLoading = rolesLoading || permissionsLoading;

  const hasLoaded = useRef(false);
  useEffect(() => {
    if (!rolesData || !permissionsData) return;
    if (!hasLoaded.current || !store.isDirty) {
      store.loadData(rolesData, permissionsData);
      hasLoaded.current = true;
    }
  }, [rolesData, permissionsData, store]);

  const {mutate: saveRoles, isPending: isSaving} = useMutation({
    ...rolesUpdateAllMutation(),
    onSuccess: (data) => {
      store.syncRolesFromServer(data);
      queryClient.invalidateQueries({queryKey: rolesGetAllQueryKey()});
      enqueueSnackbar("Роли сохранены", {variant: "success"});
    },
  });

  function handleSave() {
    saveRoles({body: store.toUpdatePayload()});
  }

  if (rolesIsError && !rolesIsRefetchError) return <QueryError error={rolesError} />;
  if (permissionsIsError && !permissionsIsRefetchError)
    return <QueryError error={permissionsError} />;

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Настройки", link: "/settings"}, {name: "Роли"}]} />
      <PageGenericHeader
        title="Роли"
        right={
          canEdit ? (
            <>
              <Button
                onClick={() => store.addRole()}
                endIcon={<AddIcon />}
                variant="outlined"
                disabled={!store.hasData}
              >
                Добавить роль
              </Button>
              <Button
                disabled={!store.isDirty || isSaving}
                onClick={() => store.reset()}
                variant="outlined"
                color="inherit"
              >
                Отменить
              </Button>
              <Button
                disabled={!store.isDirty || !store.isValid || !store.hasData || isSaving}
                onClick={handleSave}
                variant="contained"
                endIcon={isSaving ? <CircularProgress size={16} color="inherit" /> : undefined}
              >
                Сохранить
              </Button>
            </>
          ) : undefined
        }
      />
      <RolesStoreProvider store={store} canEdit={canEdit}>
        <RolesTable isLoading={isLoading} />
      </RolesStoreProvider>
    </Stack>
  );
});
