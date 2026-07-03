import {Alert, Box, Button, CircularProgress, Paper, Stack} from "@mui/material";
import {Controller, useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {ordersCreateDirectMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import WarehousesSelect from "@/components/WarehousesSelect";

type CreateDirectFormValues = {
  warehouseId: string | null;
  plannedShipmentAt: string;
  notes: string;
};

function OrderDirectCreatePage() {
  const navigate = useNavigate();

  const form = useForm<CreateDirectFormValues>({
    defaultValues: {
      warehouseId: null,
      plannedShipmentAt: "",
      notes: "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...ordersCreateDirectMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/operations/orders/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!values.warehouseId) return;
    mutation.mutate({
      body: {
        warehouseId: values.warehouseId,
        plannedShipmentAt: values.plannedShipmentAt || null,
        notes: values.notes || null,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Прямые заказы", link: "/operations/orders/direct"}, {name: "Новый заказ"}]}
      />
      <PageGenericHeader title="Новый прямой заказ" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <Controller
              control={form.control}
              name="warehouseId"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <WarehousesSelect
                  value={field.value}
                  onChange={field.onChange}
                  disabled={mutation.isPending}
                  textFieldProps={{
                    label: "Склад",
                    error: !!fieldState.error,
                    helperText: fieldState.error?.message,
                  }}
                  fullWidth
                />
              )}
            />
            <FormTextField
              control={form.control}
              name="plannedShipmentAt"
              label="Плановая дата отгрузки"
              type="datetime-local"
              disabled={mutation.isPending}
              fullWidth
              slotProps={{inputLabel: {shrink: true}}}
            />
            <FormTextField
              control={form.control}
              name="notes"
              label="Заметки"
              multiline
              rows={3}
              disabled={mutation.isPending}
              fullWidth
            />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button
                onClick={() => navigate("/operations/orders/direct")}
                disabled={mutation.isPending}
              >
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

export default OrderDirectCreatePage;
