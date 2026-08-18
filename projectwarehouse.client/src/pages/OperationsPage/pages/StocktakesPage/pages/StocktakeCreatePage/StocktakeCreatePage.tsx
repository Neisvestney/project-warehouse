import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
} from "@mui/material";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {stocktakesCreateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import WarehousesSelect from "@/components/WarehousesSelect";
import {STOCKTAKE_TYPE_LABELS} from "@/components/stocktakes/stocktakeUtils";
import type {StocktakeType} from "@/api/types.gen";

type CreateFormValues = {
  name: string;
  warehouseId: string | null;
  type: StocktakeType;
  plannedDate: string;
  notes: string;
};

function StocktakeCreatePage() {
  const navigate = useNavigate();

  const form = useForm<CreateFormValues>({
    defaultValues: {
      name: "",
      warehouseId: null,
      type: "unscheduled",
      plannedDate: "",
      notes: "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const type = useWatch({control: form.control, name: "type"});

  const mutation = useMutation({
    ...stocktakesCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/operations/stocktakes/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!values.warehouseId) return;
    mutation.mutate({
      body: {
        name: values.name || null,
        warehouseId: values.warehouseId,
        type: values.type,
        plannedDate: values.type === "scheduled" ? values.plannedDate || null : null,
        notes: values.notes || null,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Инвентаризации", link: "/operations/stocktakes"},
          {name: "Новая инвентаризация"},
        ]}
      />
      <PageGenericHeader title="Новая инвентаризация" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <FormTextField
              control={form.control}
              name="name"
              label="Название"
              disabled={mutation.isPending}
              fullWidth
              autoFocus
            />
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
            <Controller
              control={form.control}
              name="type"
              render={({field}) => (
                <TextField {...field} select label="Тип" disabled={mutation.isPending} fullWidth>
                  {(Object.keys(STOCKTAKE_TYPE_LABELS) as StocktakeType[]).map((t) => (
                    <MenuItem key={t} value={t}>
                      {STOCKTAKE_TYPE_LABELS[t]}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />
            {type === "scheduled" && (
              <FormTextField
                control={form.control}
                name="plannedDate"
                rules={{required: "Обязательное поле"}}
                label="Плановая дата"
                type="date"
                disabled={mutation.isPending}
                fullWidth
                slotProps={{inputLabel: {shrink: true}}}
              />
            )}
            <FormTextField
              control={form.control}
              name="notes"
              label="Примечания"
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
                onClick={() => navigate("/operations/stocktakes")}
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

export default StocktakeCreatePage;
