import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControlLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useState} from "react";
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {
  marketplacesCreateAccountMutation,
  marketplacesDiscoverYandexCampaignsMutation,
} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageTitle from "@/components/PageTitle.tsx";
import PageGenericHeader from "@/components/PageGenericHeader";
import TestConnectionButton from "../../components/TestConnectionButton";
import {
  MARKETPLACE_API_KEY_LABELS,
  MARKETPLACE_REQUIRES_CLIENT_ID,
  MARKETPLACE_TYPE_LABELS,
} from "@/components/marketplace/marketplaceUtils.ts";
import type {MarketplaceType, YandexMarketCampaignDto} from "@/api/types.gen";

type CreateFormValues = {
  type: MarketplaceType;
  clientId: string;
  shortName: string;
  apiKey: string;
  syncIntervalMinutes: number;
  isActive: boolean;
  campaignId: string;
};

const AVAILABLE_TYPES: MarketplaceType[] = ["ozon", "wildberries", "yandexMarket"];

function MarketplaceAccountCreatePage() {
  const navigate = useNavigate();
  const [campaignResult, setCampaignResult] = useState<{
    key: string;
    campaigns: YandexMarketCampaignDto[];
  } | null>(null);

  const form = useForm<CreateFormValues>({
    defaultValues: {
      type: "ozon",
      clientId: "",
      shortName: "",
      apiKey: "",
      syncIntervalMinutes: 30,
      isActive: true,
      campaignId: "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const [type, clientId, apiKey] = useWatch({
    control: form.control,
    name: ["type", "clientId", "apiKey"],
  });

  const mutation = useMutation({
    ...marketplacesCreateAccountMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => navigate(`/marketplaces/${data.id}`),
    onError: setApiError,
  });
  const campaignMutation = useMutation({
    ...marketplacesDiscoverYandexCampaignsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data, variables) => {
      setCampaignResult({key: variables.body.apiKey, campaigns: data});
      form.setValue("campaignId", "");
    },
    onError: setApiError,
  });
  const campaigns = campaignResult?.key === apiKey ? campaignResult.campaigns : [];

  const requiresClientId = MARKETPLACE_REQUIRES_CLIENT_ID[type];

  const onSubmit = form.handleSubmit((values) => {
    if (
      values.type === "yandexMarket" &&
      !campaigns.some((c) => c.campaignId === Number(values.campaignId))
    ) {
      form.setError("campaignId", {message: "Загрузите кампании и выберите магазин"});
      return;
    }
    mutation.mutate({
      body: {
        type: values.type,
        clientId: (MARKETPLACE_REQUIRES_CLIENT_ID[values.type] && values.clientId) || null,
        shortName: values.shortName.trim() || null,
        apiKey: values.apiKey,
        campaignId: values.type === "yandexMarket" ? Number(values.campaignId) : null,
        // input[type=number] отдаёт строку — сервер ждёт int и строку не примет
        syncIntervalMinutes: Number(values.syncIntervalMinutes),
        isActive: values.isActive,
      },
    });
  });

  return (
    <Stack spacing={2}>
      <PageTitle title="Подключение магазина" />
      <AppBreadcrumbs
        path={[{name: "Маркетплейсы", link: "/marketplaces"}, {name: "Подключение магазина"}]}
      />
      <PageGenericHeader title="Подключение магазина" />
      <Paper>
        <Box component="form" onSubmit={onSubmit} sx={{p: 3}}>
          <Stack spacing={2.5}>
            <Controller
              control={form.control}
              name="type"
              render={({field}) => (
                <Stack spacing={0.5}>
                  <Typography variant="body2" color="text.secondary">
                    Площадка
                  </Typography>
                  <Select {...field} size="small" fullWidth disabled={mutation.isPending}>
                    {AVAILABLE_TYPES.map((t) => (
                      <MenuItem key={t} value={t}>
                        {MARKETPLACE_TYPE_LABELS[t]}
                      </MenuItem>
                    ))}
                  </Select>
                </Stack>
              )}
            />
            {requiresClientId && (
              <FormTextField
                control={form.control}
                name="clientId"
                label="Client-Id"
                rules={{required: "Обязательное поле"}}
                disabled={mutation.isPending}
                fullWidth
                autoFocus
              />
            )}
            <FormTextField
              control={form.control}
              name="apiKey"
              label={MARKETPLACE_API_KEY_LABELS[type]}
              type="password"
              rules={{required: "Обязательное поле"}}
              disabled={mutation.isPending}
              fullWidth
            />
            {type === "yandexMarket" && (
              <Stack spacing={1}>
                <Button
                  variant="outlined"
                  disabled={!apiKey.trim() || campaignMutation.isPending || mutation.isPending}
                  onClick={() => {
                    form.clearErrors();
                    campaignMutation.mutate({body: {apiKey}});
                  }}
                >
                  {campaignMutation.isPending ? "Загрузка кампаний…" : "Загрузить кампании"}
                </Button>
                <Controller
                  control={form.control}
                  name="campaignId"
                  render={({field, fieldState}) => (
                    <Stack spacing={0.5}>
                      <Typography variant="body2" color="text.secondary">
                        Кампания FBS
                      </Typography>
                      <Select
                        {...field}
                        value={
                          campaigns.some((c) => String(c.campaignId) === field.value)
                            ? field.value
                            : ""
                        }
                        displayEmpty
                        size="small"
                        disabled={campaigns.length === 0 || mutation.isPending}
                        error={!!fieldState.error}
                      >
                        <MenuItem value="" disabled>
                          Выберите магазин
                        </MenuItem>
                        {campaigns.map((c) => (
                          <MenuItem key={c.campaignId} value={String(c.campaignId)}>
                            {c.name} · {c.campaignId} · {c.businessName}
                          </MenuItem>
                        ))}
                      </Select>
                      {fieldState.error && (
                        <Typography color="error" variant="caption">
                          {fieldState.error.message}
                        </Typography>
                      )}
                    </Stack>
                  )}
                />
                {campaignResult?.key === apiKey && campaigns.length === 0 && (
                  <Alert severity="info">У ключа нет доступных кампаний FBS.</Alert>
                )}
                <Typography variant="body2" color="text.secondary">
                  Каждый магазин подключается отдельно. Кампанию подключённого магазина изменить
                  нельзя.
                </Typography>
              </Stack>
            )}
            <FormTextField
              control={form.control}
              name="shortName"
              label="Короткое название"
              rules={{maxLength: {value: 32, message: "Максимум 32 символа"}}}
              helperText="Печатается на этикетках. Если пусто — печатается полное название"
              disabled={mutation.isPending}
              fullWidth
            />
            <Controller
              control={form.control}
              name="syncIntervalMinutes"
              rules={{
                required: "Обязательное поле",
                min: {value: 1, message: "Минимум 1 минута"},
                max: {value: 10080, message: "Максимум 10080 минут"},
              }}
              render={({field: f, fieldState}) => (
                <ClampedIntegerField
                  name={f.name}
                  inputRef={f.ref}
                  value={f.value}
                  onCommit={f.onChange}
                  max={10080}
                  label="Интервал синхронизации, мин"
                  disabled={mutation.isPending}
                  fullWidth
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                />
              )}
            />
            <Controller
              control={form.control}
              name="isActive"
              render={({field}) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(e) => field.onChange(e.target.checked)}
                      disabled={mutation.isPending}
                    />
                  }
                  label="Синхронизировать по расписанию"
                />
              )}
            />
            <Alert severity="info">
              Название магазина заполнится автоматически из данных площадки при первой синхронизации
              — вводить его вручную не нужно.
            </Alert>
            <TestConnectionButton
              accountId="new"
              type={type}
              clientId={requiresClientId ? clientId : ""}
              apiKey={apiKey}
              disabled={mutation.isPending}
            />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button onClick={() => navigate("/marketplaces")} disabled={mutation.isPending}>
                Отмена
              </Button>
              <Button type="submit" variant="contained" disabled={mutation.isPending}>
                {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Подключить"}
              </Button>
            </Stack>
          </Stack>
        </Box>
      </Paper>
    </Stack>
  );
}

export default MarketplaceAccountCreatePage;
