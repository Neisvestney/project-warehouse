import {useCallback, useEffect, useRef, useState} from "react";
import {observer} from "mobx-react-lite";
import {Link, useParams} from "react-router";
import {Box, Button, CircularProgress, Paper, Stack} from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import {useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  warehousesGetByIdOptions,
  warehousesGetByIdQueryKey,
  warehousesUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import {byOperation} from "@/utils/queryKeys";
import {useEditLock} from "@/hooks/useEditLock";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import EditLockBanner from "@/components/EditLockBanner";
import StaleDataBanner from "@/components/StaleDataBanner";
import PageGenericHeader from "@/components/PageGenericHeader";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import {isNotFoundError} from "@/utils/errorUtils";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {WarehouseEditStore, type WarehouseMetaFormValues} from "./warehouseEditStore";
import {WarehouseEditStoreProvider} from "./WarehouseEditStoreContext";
import WarehouseMetaForm from "./components/WarehouseMetaForm";
import WarehouseEditToolbar from "./components/WarehouseEditToolbar";
import WarehouseCanvas from "./components/WarehouseCanvas";
import DeleteWarehouseDialog from "./components/DeleteWarehouseDialog";

export default observer(function WarehouseEditPage() {
  const {id} = useParams<{id: string}>();
  const {enqueueSnackbar} = useSnackbar();
  const queryClient = useQueryClient();

  const [store] = useState(() => new WarehouseEditStore());
  const [deleteOpen, setDeleteOpen] = useState(false);

  const form = useForm<WarehouseMetaFormValues>({
    defaultValues: {name: "", width: 10, height: 10, defaultStoragePlaceNodeId: null},
  });

  // Connect ObservableForm bridge to RHF — runs once on mount
  useEffect(() => {
    return store.form.init({
      getValues: form.getValues,
      setValue: form.setValue,
      reset: form.reset,
      watch: form.watch,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const {
    data: warehouse,
    isLoading,
    isError,
    isRefetchError,
    error,
    dataUpdatedAt,
  } = useQuery({
    ...warehousesGetByIdOptions({path: {id: id!}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  // Load warehouse data into store once after initial fetch
  const hasLoaded = useRef(false);
  useEffect(() => {
    if (!warehouse || hasLoaded.current) return;
    store.loadFromDto(warehouse);
    hasLoaded.current = true;
  }, [warehouse, store]);

  // The store is loaded once, so a refetch alone would not reach the canvas — reloading it is what
  // "Обновить" has to mean here.
  const refreshWarehouse = useCallback(() => {
    hasLoaded.current = false;
    void queryClient.invalidateQueries({
      queryKey: byOperation("warehousesGetById", {path: {id: id!}}),
    });
  }, [queryClient, id]);

  // Always "dirty": the canvas holds unsaved layout the moment this page opens, and silently
  // refetching under it would drop whatever was dragged.
  const lock = useEditLock("warehouse", id, {
    isDirty: true,
    dataUpdatedAt,
    onRefresh: refreshWarehouse,
  });

  const {mutate: updateWarehouse, isPending: isSaving} = useMutation({
    ...warehousesUpdateMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({queryKey: warehousesGetByIdQueryKey({path: {id: id!}})});
      enqueueSnackbar("Склад сохранён", {variant: "success"});
    },
  });

  const onSubmit = form.handleSubmit(() => {
    updateWarehouse({path: {id: id!}, body: store.toUpdateRequest()});
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
  if (!warehouse) return <NotFound />;

  return (
    <WarehouseEditStoreProvider store={store}>
      <Stack spacing={2}>
        <AppBreadcrumbs
          path={[
            {name: "Склады", link: "/storage/warehouses"},
            {name: warehouse.name, link: `/storage/warehouses/${id}`},
            {name: "Редактировать"},
          ]}
        />
        <PageGenericHeader
          viewersOf={{entityType: "warehouse", entityId: id}}
          title="Редактировать склад"
          right={
            <>
              <Button
                component={Link}
                to={`/storage/warehouses/${id}`}
                replace
                startIcon={<ArrowBackIcon />}
                variant="outlined"
                color="inherit"
                disabled={isSaving}
              >
                Назад
              </Button>
              <Button
                onClick={() => setDeleteOpen(true)}
                variant="outlined"
                color="error"
                startIcon={<DeleteIcon />}
                disabled={isSaving}
              >
                Удалить
              </Button>
              <Button
                onClick={() => onSubmit()}
                variant="contained"
                disabled={isSaving}
                endIcon={isSaving ? <CircularProgress size={16} color="inherit" /> : undefined}
              >
                Сохранить
              </Button>
            </>
          }
        />

        <EditLockBanner heldBy={lock.heldBy} />
        <StaleDataBanner
          isStale={!lock.heldBy && lock.isStale}
          staleBy={lock.staleBy}
          onRefresh={lock.refresh}
          onDismiss={lock.dismissStale}
        />

        <Paper sx={{px: 3, py: 2}}>
          <WarehouseMetaForm control={form.control} disabled={isSaving} warehouseId={id!} />
        </Paper>

        <Stack spacing={1}>
          <WarehouseEditToolbar />
          <WarehouseCanvas />
        </Stack>
      </Stack>
      <DeleteWarehouseDialog
        open={deleteOpen}
        warehouseId={id!}
        warehouseName={warehouse.name}
        onClose={() => setDeleteOpen(false)}
      />
    </WarehouseEditStoreProvider>
  );
});
