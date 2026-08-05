import {useEffect, useRef} from "react";
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
} from "@mui/material";
import {Controller, useForm, useWatch} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesGetAccountQueryKey,
  marketplacesUpdateAccountMutation,
} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import TestConnectionButton from "../../components/TestConnectionButton";
import type {MarketplaceAccountDto} from "@/api/types.gen";

type EditFormValues = {
  clientId: string;
  apiKey: string;
  syncIntervalMinutes: number;
  isActive: boolean;
};

interface EditAccountDialogProps {
  open: boolean;
  account: MarketplaceAccountDto;
  onClose: () => void;
}

function EditAccountDialog({open, account, onClose}: EditAccountDialogProps) {
  const queryClient = useQueryClient();

  const form = useForm<EditFormValues>({
    defaultValues: {
      clientId: account.externalClientId ?? "",
      apiKey: "",
      syncIntervalMinutes: account.syncIntervalMinutes,
      isActive: account.isActive,
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  // Только на открытии: во время идущей синхронизации родитель перезапрашивает аккаунт
  // каждые 3 с, и сброс по любому изменению account затирал бы уже введённый ключ
  const {reset} = form;
  const wasOpenRef = useRef(false);
  useEffect(() => {
    if (open && !wasOpenRef.current) {
      reset({
        clientId: account.externalClientId ?? "",
        apiKey: "",
        syncIntervalMinutes: account.syncIntervalMinutes,
        isActive: account.isActive,
      });
    }
    wasOpenRef.current = open;
  }, [open, account, reset]);

  const [clientId, apiKey] = useWatch({control: form.control, name: ["clientId", "apiKey"]});

  const mutation = useMutation({
    ...marketplacesUpdateAccountMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      queryClient.setQueryData(marketplacesGetAccountQueryKey({path: {id: account.id}}), data);
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) =>
    mutation.mutate({
      path: {id: account.id},
      body: {
        clientId: values.clientId || null,
        // пустое значение означает «оставить текущий ключ»
        apiKey: values.apiKey || null,
        syncIntervalMinutes: Number(values.syncIntervalMinutes),
        isActive: values.isActive,
      },
    }),
  );

  return (
    <Dialog open={open} onClose={mutation.isPending ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Настройки подключения</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{pt: 1}}>
          <FormTextField
            control={form.control}
            name="clientId"
            label="Client-Id"
            rules={{required: "Обязательное поле"}}
            disabled={mutation.isPending}
            fullWidth
          />
          <FormTextField
            control={form.control}
            name="apiKey"
            label="Новый Api-Key"
            type="password"
            helperText={`Текущий ключ ${account.apiKeyMask}. Оставьте пустым, чтобы не менять`}
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
          {apiKey && (
            <TestConnectionButton
              accountId={account.id}
              type={account.type}
              clientId={clientId}
              apiKey={apiKey}
              disabled={mutation.isPending}
            />
          )}
          {form.formState.errors.root && (
            <Alert severity="error">{form.formState.errors.root.message}</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={onSubmit} disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default EditAccountDialog;
