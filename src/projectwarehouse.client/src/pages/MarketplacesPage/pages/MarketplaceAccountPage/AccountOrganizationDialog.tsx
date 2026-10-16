import {useState} from "react";
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
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  marketplacesGetAccountQueryKey,
  marketplacesSetAccountOrganizationMutation,
} from "@/api/@tanstack/react-query.gen";
import type {MarketplaceAccountDto} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {extractErrorMessage} from "@/utils/errorUtils";
import {byOperation} from "@/utils/queryKeys";
import OrganizationsSelect from "@/components/organizations/OrganizationsSelect";

interface AccountOrganizationDialogProps {
  open: boolean;
  account: MarketplaceAccountDto;
  onClose: () => void;
}

function AccountOrganizationDialog({open, account, onClose}: AccountOrganizationDialogProps) {
  const [shownAccount, releaseShownAccount] = useRetainedValue(open ? account : null);

  return (
    <Dialog
      open={open}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownAccount},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      {shownAccount && (
        <AccountOrganizationContent account={shownAccount} open={open} onClose={onClose} />
      )}
    </Dialog>
  );
}

interface AccountOrganizationContentProps {
  account: MarketplaceAccountDto;
  open: boolean;
  onClose: () => void;
}

function AccountOrganizationContent({account, open, onClose}: AccountOrganizationContentProps) {
  const queryClient = useQueryClient();
  const [byInn, setByInn] = useState(!account.isOrganizationLinkedManually);
  const [organizationId, setOrganizationId] = useState<string | null>(
    account.organizationId ?? null,
  );

  const mutation = useMutation({
    ...marketplacesSetAccountOrganizationMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async (data) => {
      queryClient.setQueryData(marketplacesGetAccountQueryKey({path: {id: account.id}}), data);
      // "by INN" may have created an organization on the server, so the picker list is stale too
      await Promise.all([
        queryClient.invalidateQueries({queryKey: byOperation("organizationsGetById")}),
        queryClient.invalidateQueries({queryKey: byOperation("organizationsGetAll")}),
        queryClient.invalidateQueries({queryKey: byOperation("organizationsGetShort")}),
      ]);
      onClose();
    },
  });

  useBackClosable(open && !mutation.isPending, onClose);

  const canSave = byInn || organizationId !== null;

  return (
    <>
      <DialogTitle>Организация аккаунта</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{pt: 1}}>
          <FormControlLabel
            control={
              <Switch
                checked={byInn}
                onChange={(e) => setByInn(e.target.checked)}
                disabled={mutation.isPending}
              />
            }
            label="Определять по ИНН"
          />
          {byInn ? (
            <Typography variant="body2" color="text.secondary">
              {account.inn
                ? `Аккаунт будет привязан к организации с ИНН ${account.inn}; если её нет, она будет создана из реквизитов продавца.`
                : "У аккаунта пока нет ИНН — привязка появится после синхронизации."}
            </Typography>
          ) : (
            <OrganizationsSelect
              value={organizationId}
              onChange={setOrganizationId}
              disabled={mutation.isPending}
              textFieldProps={{helperText: "Синхронизация не будет менять эту привязку"}}
            />
          )}
          {mutation.isError && (
            <Alert severity="error">
              {extractErrorMessage(mutation.error) || "Не удалось сохранить привязку"}
            </Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          disabled={!canSave || mutation.isPending}
          onClick={() =>
            mutation.mutate({
              path: {id: account.id},
              body: {organizationId: byInn ? null : organizationId},
            })
          }
        >
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </>
  );
}

export default AccountOrganizationDialog;
