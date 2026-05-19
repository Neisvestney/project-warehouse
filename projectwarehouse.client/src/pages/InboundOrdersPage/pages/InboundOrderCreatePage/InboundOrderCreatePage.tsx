import {Alert, Box, Button, CircularProgress, Paper, Stack, TextField} from "@mui/material";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useNavigate} from "react-router";
import {useMutation} from "@tanstack/react-query";
import {inboundOrdersCreateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import WarehousesSelect from "@/components/WarehousesSelect";
import UsersSelect from "@/components/UsersSelect";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";

type CreateFormValues = {
  title: string;
  warehouseId: string | null;
  plannedStartDateTime: string;
  notes: string;
  assignedUserIds: string[];
};

function InboundOrderCreatePage() {
  const navigate = useNavigate();

  const form = useForm<CreateFormValues>({
    defaultValues: {
      title: "",
      warehouseId: null,
      plannedStartDateTime: "",
      notes: "",
      assignedUserIds: [],
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const warehouseId = useWatch({control: form.control, name: "warehouseId"});

  const mutation = useMutation({
    ...inboundOrdersCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/inbound-orders/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      body: {
        warehouseId: values.warehouseId!,
        title: values.title || null,
        plannedStartDateTime: new Date(values.plannedStartDateTime).toISOString(),
        notes: values.notes || null,
        assignedUserIds: values.assignedUserIds,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Приходные ордера", link: "/inbound-orders"}, {name: "Создать"}]}
      />
      <PageGenericHeader title="Создать ордер" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <FormTextField
              control={form.control}
              name="title"
              label="Название"
              disabled={mutation.isPending}
              fullWidth
            />
            <Controller
              control={form.control}
              name="warehouseId"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <WarehousesSelect
                  value={field.value}
                  onChange={(id) => {
                    field.onChange(id);
                    form.setValue("assignedUserIds", []);
                  }}
                  disabled={mutation.isPending}
                  textFieldProps={{
                    error: !!fieldState.error,
                    helperText: fieldState.error?.message,
                  }}
                  fullWidth
                />
              )}
            />
            <Controller
              control={form.control}
              name="plannedStartDateTime"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <TextField
                  {...field}
                  label="Дата начала"
                  type="datetime-local"
                  disabled={mutation.isPending}
                  fullWidth
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                  slotProps={{inputLabel: {shrink: true}}}
                />
              )}
            />
            <FormTextField
              control={form.control}
              name="notes"
              label="Примечания"
              multiline
              minRows={3}
              disabled={mutation.isPending}
              fullWidth
            />
            <Controller
              control={form.control}
              name="assignedUserIds"
              render={({field}) => (
                <UsersSelect
                  value={field.value}
                  onChange={field.onChange}
                  warehouseId={warehouseId}
                  disabled={mutation.isPending}
                  fullWidth
                />
              )}
            />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button onClick={() => navigate("/inbound-orders")} disabled={mutation.isPending}>
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

export default InboundOrderCreatePage;
