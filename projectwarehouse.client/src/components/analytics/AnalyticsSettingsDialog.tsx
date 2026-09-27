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
  MenuItem,
  Stack,
  Typography,
} from "@mui/material";
import {
  analyticsGetAbcQueryKey,
  analyticsGetChannelsSummaryQueryKey,
  analyticsGetSettingsOptions,
  analyticsGetSettingsQueryKey,
  analyticsUpdateSettingsMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  AnalyticsSettingsDto,
  AnalyticsUpdateSettingsError,
  AnalyticsXyzStep,
  UpdateAnalyticsSettingsRequest,
} from "@/api/types.gen";
import {FormTextField} from "@/components/form/FormTextField";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

const MIN_RETURNS_MATURITY_DAYS = 1;
const MAX_RETURNS_MATURITY_DAYS = 120;

/** Which page opened the dialog; each shows only the parameters its numbers depend on. */
export type AnalyticsSettingsGroup = "channels" | "abc";

const GROUP_TITLES: Record<AnalyticsSettingsGroup, string> = {
  channels: "Настройки сводки по каналам",
  abc: "Настройки ABC / XYZ",
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
        queryClient.invalidateQueries({queryKey: analyticsGetAbcQueryKey()}),
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
        ) : shownGroup === "abc" ? (
          <AbcSettingsContent
            settings={settings}
            isPending={isPending}
            onSave={(body, onError) => mutation.mutate({body}, {onError})}
            onClose={handleClose}
          />
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

const XYZ_STEP_LABELS: Record<AnalyticsXyzStep, string> = {
  week: "неделя",
  month: "месяц",
};

interface AbcFormValues {
  abcBoundaryA: string;
  abcBoundaryB: string;
  xyzBoundaryX: string;
  xyzBoundaryY: string;
  xyzStep: AnalyticsXyzStep | "";
  xyzMinIntervals: string;
}

type AbcNumberField = Exclude<keyof AbcFormValues, "xyzStep">;

/** Mirrors the server ranges; the order of a pair is checked against the value that will apply. */
const ABC_FIELDS: Record<
  AbcNumberField,
  {label: string; min: number; max: number; integer?: boolean; helper: string}
> = {
  abcBoundaryA: {
    label: "Граница A, %",
    min: 1,
    max: 99,
    helper: "Накопленная доля, до которой позиция в A",
  },
  abcBoundaryB: {label: "Граница B, %", min: 2, max: 99.9, helper: "Больше границы A"},
  xyzBoundaryX: {
    label: "Граница X, %",
    min: 1,
    max: 999,
    helper: "Коэффициент вариации, до которого спрос стабилен",
  },
  xyzBoundaryY: {label: "Граница Y, %", min: 2, max: 1000, helper: "Больше границы X"},
  xyzMinIntervals: {
    label: "Минимум интервалов XYZ",
    min: 2,
    max: 52,
    integer: true,
    helper: "Меньше полных интервалов в периоде — XYZ не считается",
  },
};

function parseDecimal(value: string): number {
  return Number(value.trim().replace(",", "."));
}

function optionalBoundary(value: string, min: number, max: number): string | true {
  if (value.trim() === "") return true;
  if (!/^\d+([.,]\d)?$/.test(value.trim())) return "Число с одним знаком после запятой или пусто";
  const n = parseDecimal(value);
  return n >= min && n <= max ? true : `Допустимо от ${min} до ${max}`;
}

function toNullableDecimal(value: string): number | null {
  return value.trim() === "" ? null : parseDecimal(value);
}

function AbcSettingsContent({settings, isPending, onSave, onClose}: ContentProps) {
  const {saved, defaults} = settings;
  const form = useForm<AbcFormValues>({
    defaultValues: {
      abcBoundaryA: saved.abcBoundaryA?.toString() ?? "",
      abcBoundaryB: saved.abcBoundaryB?.toString() ?? "",
      xyzBoundaryX: saved.xyzBoundaryX?.toString() ?? "",
      xyzBoundaryY: saved.xyzBoundaryY?.toString() ?? "",
      xyzStep: saved.xyzStep ?? "",
      xyzMinIntervals: saved.xyzMinIntervals?.toString() ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const {control, formState} = form;

  const applied = (values: AbcFormValues, field: "abcBoundaryA" | "xyzBoundaryX") =>
    toNullableDecimal(values[field]) ?? defaults[field];

  const onSubmit = form.handleSubmit((values) => {
    onSave(
      {
        ...saved,
        abcBoundaryA: toNullableDecimal(values.abcBoundaryA),
        abcBoundaryB: toNullableDecimal(values.abcBoundaryB),
        xyzBoundaryX: toNullableDecimal(values.xyzBoundaryX),
        xyzBoundaryY: toNullableDecimal(values.xyzBoundaryY),
        xyzStep: values.xyzStep || null,
        xyzMinIntervals: toNullableNumber(values.xyzMinIntervals),
        version: settings.version,
      },
      setApiError,
    );
  });

  const numberField = (name: AbcNumberField, orderAfter?: "abcBoundaryA" | "xyzBoundaryX") => {
    const {label, min, max, integer, helper} = ABC_FIELDS[name];
    return (
      <FormTextField
        control={control}
        name={name}
        label={label}
        placeholder={String(defaults[name])}
        helperText={`${helper}. По умолчанию ${defaults[name]}`}
        size="small"
        fullWidth
        disabled={isPending}
        rules={{
          validate: (v, values) => {
            const text = String(v ?? "");
            const range = integer
              ? optionalInteger(text, min, max)
              : optionalBoundary(text, min, max);
            if (range !== true || !orderAfter) return range;
            const own = toNullableDecimal(text) ?? defaults[name];
            return own > applied(values, orderAfter)
              ? true
              : `Должна быть больше границы ${orderAfter === "abcBoundaryA" ? "A" : "X"}`;
          },
        }}
      />
    );
  };

  return (
    <>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <Typography variant="body2" color="text.secondary">
            Настройки общие для всех пользователей и сразу перекрашивают классы любого периода.
            Пустое поле — системное значение по умолчанию.
          </Typography>

          {numberField("abcBoundaryA")}
          {numberField("abcBoundaryB", "abcBoundaryA")}
          {numberField("xyzBoundaryX")}
          {numberField("xyzBoundaryY", "xyzBoundaryX")}

          <FormTextField
            control={control}
            name="xyzStep"
            label="Шаг XYZ"
            select
            size="small"
            fullWidth
            disabled={isPending}
            helperText={`Интервал, по которому считается вариация. По умолчанию — ${XYZ_STEP_LABELS[defaults.xyzStep]}`}
          >
            <MenuItem value="">По умолчанию</MenuItem>
            <MenuItem value="week">{XYZ_STEP_LABELS.week}</MenuItem>
            <MenuItem value="month">{XYZ_STEP_LABELS.month}</MenuItem>
          </FormTextField>

          {numberField("xyzMinIntervals")}

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
