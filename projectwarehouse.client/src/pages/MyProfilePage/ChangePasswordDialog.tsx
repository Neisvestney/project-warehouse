import {useEffect} from "react";
import {Controller, useForm} from "react-hook-form";
import {
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormHelperText,
  Stack,
  TextField,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useMutation} from "@tanstack/react-query";
import {authChangeOwnPasswordMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

interface ChangePasswordDialogProps {
  open: boolean;
  onClose: () => void;
}

type FormValues = {newPassword: string; currentPassword: string};

function ChangePasswordDialog({open, onClose}: ChangePasswordDialogProps) {
  const form = useForm<FormValues>({defaultValues: {newPassword: ""}});
  const {setApiError} = useRhfApiErrors(form);

  const mutation = useMutation({
    ...authChangeOwnPasswordMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: () => {
      form.reset();
      onClose();
    },
    onError: setApiError,
  });

  useEffect(() => {
    if (!open) form.reset();
  }, [open, form]);

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      body: {newPassword: values.newPassword, currentPassword: values.currentPassword},
    });
  });

  useBackClosable(open && !mutation.isPending, onClose);

  return (
    <Dialog open={open} onClose={mutation.isPending ? undefined : onClose} fullWidth maxWidth="xs">
      <DialogTitle>Смена пароля</DialogTitle>
      <form onSubmit={onSubmit}>
        <DialogContent>
          <Stack spacing={2} sx={{mt: 0.5}}>
            <Controller
              control={form.control}
              name="currentPassword"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <TextField
                  {...field}
                  label="Текущий пароль"
                  type="password"
                  fullWidth
                  autoFocus
                  autoComplete="current-password"
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                  disabled={mutation.isPending}
                />
              )}
            />
            <Controller
              control={form.control}
              name="newPassword"
              rules={{required: "Обязательное поле"}}
              render={({field, fieldState}) => (
                <TextField
                  {...field}
                  label="Новый пароль"
                  type="password"
                  fullWidth
                  autoFocus
                  autoComplete="new-password"
                  error={!!fieldState.error}
                  helperText={fieldState.error?.message}
                  disabled={mutation.isPending}
                />
              )}
            />
            {form.formState.errors.root && (
              <FormHelperText error>{form.formState.errors.root.message}</FormHelperText>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Отмена
          </Button>
          <Button type="submit" variant="contained" disabled={mutation.isPending}>
            {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

export default ChangePasswordDialog;
