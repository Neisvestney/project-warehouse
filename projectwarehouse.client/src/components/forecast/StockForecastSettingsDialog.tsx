import {Controller, useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import {
  stockForecastGetListQueryKey,
  stockForecastGetSettingsOptions,
  stockForecastGetSettingsQueryKey,
  stockForecastUpdateSettingsMutation,
} from "@/api/@tanstack/react-query.gen";
import {FormTextField} from "@/components/form/FormTextField";
import {FormTimeZoneField} from "@/components/form/FormTimeZoneField";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

const MAX_WARNING_DAYS = 3650;
const MIN_WINDOW_DAYS = 1;
const MAX_WINDOW_DAYS = 366;

interface SettingsFormValues {
  stockWarningDays: string;
  consumptionWindowDays: string;
  useWeightedConsumption: boolean;
  timeZoneId: string | null;
}

const EMPTY_VALUES: SettingsFormValues = {
  stockWarningDays: "",
  consumptionWindowDays: "",
  useWeightedConsumption: false,
  timeZoneId: null,
};

function optionalDays(value: string, min: number, max: number): string | true {
  if (value.trim() === "") return true;
  if (!/^\d+$/.test(value.trim())) return "Целое число или пусто";
  const n = Number(value);
  return n >= min && n <= max ? true : `Допустимо от ${min} до ${max}`;
}

function toNullableNumber(value: string): number | null {
  return value.trim() === "" ? null : Number(value);
}

interface StockForecastSettingsDialogProps {
  open: boolean;
  warehouseId: string;
  onClose: () => void;
}

export function StockForecastSettingsDialog({
  open,
  warehouseId,
  onClose,
}: StockForecastSettingsDialogProps) {
  const queryClient = useQueryClient();

  const {data: settings, isLoading} = useQuery({
    ...stockForecastGetSettingsOptions({path: {warehouseId}}),
    enabled: open,
  });

  const form = useForm<SettingsFormValues>({
    defaultValues: EMPTY_VALUES,
    values: settings && {
      stockWarningDays: settings.stockWarningDays?.toString() ?? "",
      consumptionWindowDays: settings.consumptionWindowDays?.toString() ?? "",
      useWeightedConsumption: settings.useWeightedConsumption,
      timeZoneId: settings.timeZoneId ?? null,
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const {control, formState, reset} = form;

  const mutation = useMutation({
    ...stockForecastUpdateSettingsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: stockForecastGetSettingsQueryKey({path: {warehouseId}}),
        }),
        queryClient.invalidateQueries({queryKey: stockForecastGetListQueryKey()}),
      ]);
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

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {warehouseId},
      body: {
        stockWarningDays: toNullableNumber(values.stockWarningDays),
        consumptionWindowDays: toNullableNumber(values.consumptionWindowDays),
        useWeightedConsumption: values.useWeightedConsumption,
        timeZoneId: values.timeZoneId?.trim() ? values.timeZoneId.trim() : null,
      },
    });
  });

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Настройки прогноза склада</DialogTitle>
      <DialogContent>
        {isLoading || !settings ? (
          <Stack sx={{alignItems: "center", py: 4}}>
            <CircularProgress size={32} />
          </Stack>
        ) : (
          <Stack spacing={2} sx={{pt: 1}}>
            <Typography variant="body2" color="text.secondary">
              Пустое поле означает системное значение по умолчанию, а не ноль.
            </Typography>

            <FormTextField
              control={control}
              name="stockWarningDays"
              label="Порог предупреждения, дней"
              placeholder={String(settings.defaultWarningDays)}
              helperText={`По умолчанию ${settings.defaultWarningDays}`}
              size="small"
              fullWidth
              disabled={isPending}
              rules={{validate: (v) => optionalDays(String(v ?? ""), 0, MAX_WARNING_DAYS)}}
            />

            <FormTextField
              control={control}
              name="consumptionWindowDays"
              label="Окно расчёта расхода, дней"
              placeholder={String(settings.defaultWindowDays)}
              helperText={`По умолчанию ${settings.defaultWindowDays}`}
              size="small"
              fullWidth
              disabled={isPending}
              rules={{
                validate: (v) => optionalDays(String(v ?? ""), MIN_WINDOW_DAYS, MAX_WINDOW_DAYS),
              }}
            />

            <FormTimeZoneField
              control={control}
              name="timeZoneId"
              placeholder={settings.effectiveTimeZoneId}
              helperText="Пусто — пояс вызывающего или сервера"
              disabled={isPending}
              size="small"
              fullWidth
            />

            <Controller
              control={control}
              name="useWeightedConsumption"
              render={({field}) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(_, checked) => field.onChange(checked)}
                      disabled={isPending}
                    />
                  }
                  label="Взвешенный расход (свежие дни весят больше)"
                />
              )}
            />

            <Alert severity="info" icon={false}>
              <Typography variant="body2">Фактически применяется:</Typography>
              <Typography variant="body2">
                порог {settings.effectiveWarningDays} дн., окно {settings.effectiveWindowDays} дн.,
                пояс {settings.effectiveTimeZoneId}
              </Typography>
            </Alert>

            {formState.errors.root && (
              <Alert severity="error">{formState.errors.root.message}</Alert>
            )}
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={isPending}>
          Отмена
        </Button>
        <Button onClick={onSubmit} variant="contained" disabled={isPending || !settings}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default StockForecastSettingsDialog;
