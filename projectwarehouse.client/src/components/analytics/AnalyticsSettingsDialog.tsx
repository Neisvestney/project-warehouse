import {useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
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
  analyticsGetChannelsSummaryQueryKey,
  analyticsGetSettingsOptions,
  analyticsGetSettingsQueryKey,
  analyticsUpdateSettingsMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  AnalyticsSettingsDto,
  AnalyticsUpdateSettingsError,
  UpdateAnalyticsSettingsRequest,
} from "@/api/types.gen";
import {FormTextField} from "@/components/form/FormTextField";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

const MIN_RETURNS_MATURITY_DAYS = 1;
const MAX_RETURNS_MATURITY_DAYS = 120;

/** Which page opened the dialog; each shows only the parameters its numbers depend on. */
export type AnalyticsSettingsGroup = "channels";

const GROUP_TITLES: Record<AnalyticsSettingsGroup, string> = {
  channels: "Настройки сводки по каналам",
};

interface ChannelsFormValues {
  returnsMaturityDays: string;
}

function optionalInteger(value: string, min: number, max: number): string | true {
  if (value.trim() === "") return true;
  if (!/^\d+$/.test(value.trim())) return "Целое число или пусто";
  const n = Number(value);
  return n >= min && n <= max ? true : `Допустимо от ${min} до ${max}`;
}

function toNullableNumber(value: string): number | null {
  return value.trim() === "" ? null : Number(value);
}

interface AnalyticsSettingsDialogProps {
  open: boolean;
  group: AnalyticsSettingsGroup;
  onClose: () => void;
}

export function AnalyticsSettingsDialog({open, group, onClose}: AnalyticsSettingsDialogProps) {
  const [shownGroup, releaseShownGroup] = useRetainedValue(open ? group : null);
  const {data: settings, isLoading} = useQuery({...analyticsGetSettingsOptions(), enabled: open});
  const queryClient = useQueryClient();

  // Owned by the shell so every way out — backdrop, Escape, Back, Cancel — waits for a running save
  const mutation = useMutation({
    ...analyticsUpdateSettingsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({queryKey: analyticsGetSettingsQueryKey()}),
        queryClient.invalidateQueries({queryKey: analyticsGetChannelsSummaryQueryKey()}),
      ]);
      onClose();
    },
  });
  const isPending = mutation.isPending;

  const handleClose = () => {
    if (!isPending) onClose();
  };

  useBackClosable(open && !isPending, handleClose);

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      maxWidth="xs"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownGroup},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownGroup && <DialogTitle>{GROUP_TITLES[shownGroup]}</DialogTitle>}
      {shownGroup &&
        (isLoading || !settings ? (
          <DialogContent>
            <Stack sx={{alignItems: "center", py: 4}}>
              <CircularProgress size={32} />
            </Stack>
          </DialogContent>
        ) : (
          <ChannelsSettingsContent
            settings={settings}
            isPending={isPending}
            onSave={(body, onError) => mutation.mutate({body}, {onError})}
            onClose={handleClose}
          />
        ))}
    </Dialog>
  );
}

interface ContentProps {
  settings: AnalyticsSettingsDto;
  isPending: boolean;
  onSave: (
    body: UpdateAnalyticsSettingsRequest,
    onError: (error: AnalyticsUpdateSettingsError) => void,
  ) => void;
  onClose: () => void;
}

function ChannelsSettingsContent({settings, isPending, onSave, onClose}: ContentProps) {
  const form = useForm<ChannelsFormValues>({
    defaultValues: {returnsMaturityDays: settings.saved.returnsMaturityDays?.toString() ?? ""},
  });
  const {setApiError} = useRhfApiErrors(form);
  const {control, formState} = form;

  // The write is a full one, so every group the dialog does not show goes back as it was saved
  const onSubmit = form.handleSubmit((values) => {
    onSave(
      {
        ...settings.saved,
        returnsMaturityDays: toNullableNumber(values.returnsMaturityDays),
        version: settings.version,
      },
      setApiError,
    );
  });

  return (
    <>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <Typography variant="body2" color="text.secondary">
            Настройки общие для всех пользователей и сразу действуют на любой период. Пустое поле —
            системное значение по умолчанию.
          </Typography>

          <FormTextField
            control={control}
            name="returnsMaturityDays"
            label="Срок созревания возвратов, дней"
            placeholder={String(settings.defaults.returnsMaturityDays)}
            helperText={`Период младше этого срока помечается звёздочкой: возвраты ещё поступают. По умолчанию ${settings.defaults.returnsMaturityDays}`}
            size="small"
            fullWidth
            disabled={isPending}
            rules={{
              validate: (v) =>
                optionalInteger(
                  String(v ?? ""),
                  MIN_RETURNS_MATURITY_DAYS,
                  MAX_RETURNS_MATURITY_DAYS,
                ),
            }}
          />

          {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button onClick={onSubmit} variant="contained" disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </>
  );
}

export default AnalyticsSettingsDialog;
