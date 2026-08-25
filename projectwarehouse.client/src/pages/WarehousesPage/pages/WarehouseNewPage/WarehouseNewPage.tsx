import {Alert, Box, Button, CircularProgress, Paper, Stack} from "@mui/material";
import {useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {warehousesCreateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import WarehouseMetaForm from "@/pages/WarehousesPage/pages/WarehouseEditPage/components/WarehouseMetaForm";
import type {WarehouseMetaFormValues} from "@/pages/WarehousesPage/pages/WarehouseEditPage/warehouseEditStore";

function WarehouseNewPage() {
  const navigate = useNavigate();

  const form = useForm<WarehouseMetaFormValues>({
    defaultValues: {
      name: "",
      width: 10,
      height: 10,
      defaultStoragePlaceNodeId: null,
      timeZoneId: null,
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...warehousesCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/storage/warehouses/${data.id}/edit`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      body: {
        name: values.name,
        width: Number(values.width),
        height: Number(values.height),
        storagePlaces: [],
        layoutObjects: [],
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs path={[{name: "Склады", link: "/storage/warehouses"}, {name: "Создать"}]} />
      <PageGenericHeader title="Создать склад" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <WarehouseMetaForm control={form.control} disabled={mutation.isPending} />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button onClick={() => navigate("/storage/warehouses")} disabled={mutation.isPending}>
                Отмена
              </Button>
              <Button type="submit" variant="contained" disabled={mutation.isPending}>
                {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Создать"}
              </Button>
            </Stack>
          </Stack>
        </Box>
      </Paper>
    </Stack>
  );
}

export default WarehouseNewPage;
