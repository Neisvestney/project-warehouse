import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import {Controller, useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {receiptsCreateMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import WarehousesSelect from "@/components/WarehousesSelect";
import {RECEIPT_REASON_LABELS} from "@/components/receipts/receiptUtils";
import type {ReceiptReason} from "@/api/types.gen";

type CreateFormValues = {
  name: string;
  reason: ReceiptReason;
  warehouseId: string | null;
  notes: string;
  plannedDeliveryDate: string;
};

const ALL_REASONS: ReceiptReason[] = ["newGoods", "return", "other"];

function ReceiptCreatePage() {
  const navigate = useNavigate();

  const form = useForm<CreateFormValues>({
    defaultValues: {
      name: "",
      reason: "newGoods",
      warehouseId: null,
      notes: "",
      plannedDeliveryDate: "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...receiptsCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/operations/receipts/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!values.warehouseId) return;
    mutation.mutate({
      body: {
        name: values.name,
        reason: values.reason,
        warehouseId: values.warehouseId,
        notes: values.notes || null,
        plannedDeliveryDate: values.plannedDeliveryDate || null,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[{name: "Приемки", link: "/operations/receipts"}, {name: "Новая приемка"}]}
      />
      <PageGenericHeader title="Новая приемка" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <FormTextField
              control={form.control}
              name="name"
              label="Название"
              rules={{required: "Обязательное поле"}}
              disabled={mutation.isPending}
              fullWidth
              autoFocus
            />
            <Controller
              control={form.control}
              name="reason"
              render={({field}) => (
                <Stack spacing={0.5}>
                  <Typography variant="body2" color="text.secondary">
                    Причина поступления
                  </Typography>
                  <Select {...field} size="small" fullWidth disabled={mutation.isPending}>
                    {ALL_REASONS.map((r) => (
                      <MenuItem key={r} value={r}>
                        {RECEIPT_REASON_LABELS[r]}
                      </MenuItem>
                    ))}
                  </Select>
                </Stack>
              )}
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
            <FormTextField
              control={form.control}
              name="plannedDeliveryDate"
              label="Планируемая дата поставки"
              type="date"
              disabled={mutation.isPending}
              fullWidth
              slotProps={{inputLabel: {shrink: true}}}
            />
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
                onClick={() => navigate("/operations/receipts")}
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

export default ReceiptCreatePage;
