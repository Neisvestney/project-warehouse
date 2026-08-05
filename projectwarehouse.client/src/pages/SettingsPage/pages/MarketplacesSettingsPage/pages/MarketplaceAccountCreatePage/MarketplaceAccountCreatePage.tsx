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
import {useMutation} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {marketplacesCreateAccountMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import AppBreadcrumbs from "@/components/AppBreadcrumbs";
import PageGenericHeader from "@/components/PageGenericHeader";
import TestConnectionButton from "../../components/TestConnectionButton";
import {MARKETPLACE_TYPE_LABELS} from "../../marketplaceUtils";
import type {MarketplaceType} from "@/api/types.gen";

type CreateFormValues = {
  type: MarketplaceType;
  clientId: string;
  apiKey: string;
  syncIntervalMinutes: number;
  isActive: boolean;
};

// Wildberries появится, когда будет свой провайдер — схема БД под него уже готова
const AVAILABLE_TYPES: MarketplaceType[] = ["ozon"];

function MarketplaceAccountCreatePage() {
  const navigate = useNavigate();

  const form = useForm<CreateFormValues>({
    defaultValues: {
      type: "ozon",
      clientId: "",
      apiKey: "",
      syncIntervalMinutes: 30,
      isActive: true,
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
    onSuccess: (data) => navigate(`/settings/integrations/${data.id}`),
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) =>
    mutation.mutate({
      body: {
        type: values.type,
        clientId: values.clientId || null,
        apiKey: values.apiKey,
        // input[type=number] отдаёт строку — сервер ждёт int и строку не примет
        syncIntervalMinutes: Number(values.syncIntervalMinutes),
        isActive: values.isActive,
      },
    }),
  );

  return (
    <Stack spacing={2}>
      <AppBreadcrumbs
        path={[
          {name: "Маркетплейсы", link: "/settings/integrations"},
          {name: "Подключение магазина"},
        ]}
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
            <FormTextField
              control={form.control}
              name="clientId"
              label="Client-Id"
              rules={{required: "Обязательное поле"}}
              disabled={mutation.isPending}
              fullWidth
              autoFocus
            />
            <FormTextField
              control={form.control}
              name="apiKey"
              label="Api-Key"
              type="password"
              rules={{required: "Обязательное поле"}}
              disabled={mutation.isPending}
              fullWidth
            />
            <FormTextField
              control={form.control}
              name="syncIntervalMinutes"
              label="Интервал синхронизации, мин"
              type="number"
              rules={{
                required: "Обязательное поле",
                min: {value: 1, message: "Минимум 1 минута"},
                max: {value: 10080, message: "Максимум 10080 минут"},
              }}
              disabled={mutation.isPending}
              fullWidth
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
              clientId={clientId}
              apiKey={apiKey}
              disabled={mutation.isPending}
            />
            {form.formState.errors.root && (
              <Alert severity="error">{form.formState.errors.root.message}</Alert>
            )}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button
                onClick={() => navigate("/settings/integrations")}
                disabled={mutation.isPending}
              >
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
