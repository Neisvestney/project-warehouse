import {useCallback, useEffect, useRef, useState} from "react";
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
import {useEditLock} from "@/hooks/useEditLock";
import AppBreadcrumbs from "@/components/AppBreadcrumbs.tsx";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import PageGenericHeader from "@/components/PageGenericHeader.tsx";
import {useHasPermission} from "@/hooks/usePermission";
import {RolesStore} from "./rolesStore";
import {RolesStoreProvider} from "./RolesStoreContext.tsx";
import RolesTable from "./RolesTable.tsx";
import QueryError from "@/components/QueryError.tsx";

/** The changelog stores roles as a single object keyed by an empty guid. */
const ROLES_ENTITY_ID = "00000000-0000-0000-0000-000000000000";

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
    dataUpdatedAt: rolesUpdatedAt,
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

  const refreshRoles = useCallback(() => {
    void queryClient.invalidateQueries({queryKey: rolesGetAllQueryKey()});
  }, [queryClient]);

  // Roles are versioned as one object: the changelog records them under an empty id, and that is the
  // key both the event and the subscription use.
  const lock = useEditLock("roles", ROLES_ENTITY_ID, {
    isDirty: store.isDirty,
    dataUpdatedAt: rolesUpdatedAt,
    onRefresh: refreshRoles,
    enabled: canEdit,
  });

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
      <AppBreadcrumbs
        path={[{name: "Настройки", link: "/settings"}, {name: "Роли"}]}
        viewersOf={{entityType: "roles", entityId: ROLES_ENTITY_ID}}
      />
      <PageGenericHeader
        title="Роли"
        actions={
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
      <EditLockBanner heldBy={lock.heldBy} />
      <StaleDataBanner
        isStale={!lock.heldBy && lock.isStale}
        staleBy={lock.staleBy}
        onRefresh={lock.refresh}
        onDismiss={lock.dismissStale}
      />

      <RolesStoreProvider store={store} canEdit={canEdit}>
        <RolesTable isLoading={isLoading} />
      </RolesStoreProvider>
    </Stack>
  );
});
