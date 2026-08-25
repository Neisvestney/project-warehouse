import {useForm} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import {
  stockForecastGetListQueryKey,
  stockForecastSetOverrideMutation,
} from "@/api/@tanstack/react-query.gen";
import {FormTextField} from "@/components/form/FormTextField";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

const MAX_WARNING_DAYS = 3650;

interface OverrideFormValues {
  warningDays: string;
}

export interface StockWarningOverrideTarget {
  catalogItemId: string;
  itemName: string;
  warningDays: number;
  isWarningOverridden: boolean;
}

interface StockWarningOverrideDialogProps {
  target: StockWarningOverrideTarget | null;
  warehouseId: string;
  /** Null while the list has not loaded — the hint is dropped rather than guessing a threshold. */
  warehouseWarningDays: number | null;
  onClose: () => void;
}

export function StockWarningOverrideDialog({
  target,
  warehouseId,
  warehouseWarningDays,
  onClose,
}: StockWarningOverrideDialogProps) {
  const queryClient = useQueryClient();

  const form = useForm<OverrideFormValues>({
    defaultValues: {warningDays: ""},
    values: target ? {warningDays: String(target.warningDays)} : undefined,
  });
  const {setApiError} = useRhfApiErrors(form);
  const {control, formState, reset} = form;

  const mutation = useMutation({
    ...stockForecastSetOverrideMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: stockForecastGetListQueryKey()});
      reset();
      onClose();
    },
    onError: setApiError,
  });

  const isPending = mutation.isPending;

  const handleClose = () => {
    if (isPending) return;
    reset();
    onClose();
  };

  const submit = (warningDays: number | null) => {
    if (!target) return;
    mutation.mutate({
      body: {warehouseId, catalogItemId: target.catalogItemId, warningDays},
    });
  };

  const onSubmit = form.handleSubmit((values) => submit(Number(values.warningDays)));

  return (
    <Dialog open={target !== null} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Порог предупреждения</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <Typography variant="body2" color="text.secondary">
            {target?.itemName}
          </Typography>

          <FormTextField
            control={control}
            name="warningDays"
            label="Порог, дней"
            size="small"
            fullWidth
            autoFocus
            disabled={isPending}
            helperText={
              warehouseWarningDays === null
                ? undefined
                : target?.isWarningOverridden
                  ? `Порог склада — ${warehouseWarningDays} дн.`
                  : `Сейчас наследуется от склада: ${warehouseWarningDays} дн.`
            }
            rules={{
              required: "Обязательное поле",
              validate: (v) => {
                const raw = String(v ?? "").trim();
                if (!/^\d+$/.test(raw)) return "Целое число";
                const n = Number(raw);
                return n <= MAX_WARNING_DAYS ? true : `Допустимо от 0 до ${MAX_WARNING_DAYS}`;
              },
            }}
          />

          {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        {/* Reset deletes the override so the item follows the warehouse again — it is not a write of the warehouse value. */}
        {target?.isWarningOverridden && (
          <Button color="warning" onClick={() => submit(null)} disabled={isPending}>
            Сбросить
          </Button>
        )}
        <Button onClick={handleClose} disabled={isPending} sx={{ml: "auto"}}>
          Отмена
        </Button>
        <Button onClick={onSubmit} variant="contained" disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default StockWarningOverrideDialog;
