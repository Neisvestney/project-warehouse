import {Alert, Button, CircularProgress, Stack} from "@mui/material";
import {useMutation} from "@tanstack/react-query";
import {marketplacesTestConnectionMutation} from "@/api/@tanstack/react-query.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import type {MarketplaceType} from "@/api/types.gen";

interface TestConnectionButtonProps {
  /** Идентификатор аккаунта; для несохранённого — любая строка, бэкенд её игнорирует при непустом apiKey. */
  accountId: string;
  type: MarketplaceType;
  clientId: string;
  apiKey: string;
  disabled?: boolean;
}

function TestConnectionButton({
  accountId,
  type,
  clientId,
  apiKey,
  disabled,
}: TestConnectionButtonProps) {
  const mutation = useMutation({
    ...marketplacesTestConnectionMutation(),
    meta: {suppressGlobalError: true},
  });

  return (
    <Stack spacing={1}>
      <Stack direction="row">
        <Button
          variant="outlined"
          disabled={disabled || !apiKey || mutation.isPending}
          onClick={() =>
            mutation.mutate({
              path: {id: accountId},
              body: {type, clientId: clientId || null, apiKey},
            })
          }
        >
          {mutation.isPending ? <CircularProgress size={22} /> : "Проверить подключение"}
        </Button>
      </Stack>
      {mutation.isSuccess && (
        <Alert severity={mutation.data.isValid ? "success" : "error"}>
          {mutation.data.message ??
            (mutation.data.isValid ? "Подключение работает" : "Маркетплейс отклонил ключ")}
        </Alert>
      )}
      {mutation.isError && <Alert severity="error">{extractErrorMessage(mutation.error)}</Alert>}
    </Stack>
  );
}

export default TestConnectionButton;
