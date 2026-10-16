import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
} from "@mui/material";
import {useForm} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  organizationsCreateMutation,
  organizationsUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import type {OrganizationDto} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import {byOperation} from "@/utils/queryKeys";

type FormValues = {
  name: string;
  inn: string;
  legalName: string;
  kpp: string;
  ogrn: string;
  ownershipForm: string;
};

/** `"new"` opens an empty form for creation. */
export type OrganizationFormTarget = OrganizationDto | "new";

interface OrganizationFormDialogProps {
  target: OrganizationFormTarget | null;
  onClose: () => void;
}

function OrganizationFormDialog({target, onClose}: OrganizationFormDialogProps) {
  const [shownTarget, releaseShownTarget] = useRetainedValue(target);

  return (
    <Dialog
      open={target !== null}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownTarget},
        paper: {sx: {pointerEvents: target ? undefined : "none"}},
      }}
    >
      {shownTarget && (
        <OrganizationFormContent target={shownTarget} open={target !== null} onClose={onClose} />
      )}
    </Dialog>
  );
}

interface OrganizationFormContentProps {
  target: OrganizationFormTarget;
  open: boolean;
  onClose: () => void;
}

function OrganizationFormContent({target, open, onClose}: OrganizationFormContentProps) {
  const queryClient = useQueryClient();
  const existing = target === "new" ? null : target;

  const form = useForm<FormValues>({
    defaultValues: {
      name: existing?.name ?? "",
      inn: existing?.inn ?? "",
      legalName: existing?.legalName ?? "",
      kpp: existing?.kpp ?? "",
      ogrn: existing?.ogrn ?? "",
      ownershipForm: existing?.ownershipForm ?? "",
    },
  });
  const {setApiError} = useRhfApiErrors(form);

  const handleSuccess = async (data: OrganizationDto) => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: byOperation("organizationsGetAll")}),
      queryClient.invalidateQueries({queryKey: byOperation("organizationsGetShort")}),
      queryClient.invalidateQueries({
        queryKey: byOperation("organizationsGetById", {path: {id: data.id}}),
      }),
    ]);
    onClose();
  };

  const createMutation = useMutation({
    ...organizationsCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: handleSuccess,
    onError: setApiError,
  });
  const updateMutation = useMutation({
    ...organizationsUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: handleSuccess,
    onError: setApiError,
  });
  const isPending = createMutation.isPending || updateMutation.isPending;

  useBackClosable(open && !isPending, onClose);

  const onSubmit = form.handleSubmit((values) => {
    const body = {
      name: values.name,
      inn: values.inn,
      legalName: values.legalName || null,
      kpp: values.kpp || null,
      ogrn: values.ogrn || null,
      ownershipForm: values.ownershipForm || null,
    };
    if (existing) updateMutation.mutate({path: {id: existing.id}, body});
    else createMutation.mutate({body});
  });

  return (
    <>
      <DialogTitle>{existing ? "Реквизиты организации" : "Новая организация"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{pt: 1}}>
          <FormTextField
            control={form.control}
            name="name"
            label="Название"
            rules={{required: "Обязательное поле"}}
            disabled={isPending}
            autoFocus
            fullWidth
          />
          <FormTextField
            control={form.control}
            name="inn"
            label="ИНН"
            rules={{
              required: "Обязательное поле",
              pattern: {value: /^(\d{10}|\d{12})$/, message: "10 или 12 цифр"},
            }}
            disabled={isPending}
            fullWidth
          />
          <FormTextField
            control={form.control}
            name="legalName"
            label="Полное наименование"
            disabled={isPending}
            fullWidth
          />
          <Stack direction={{xs: "column", sm: "row"}} spacing={2}>
            <FormTextField
              control={form.control}
              name="kpp"
              label="КПП"
              rules={{pattern: {value: /^\d{9}$/, message: "9 цифр"}}}
              disabled={isPending}
              fullWidth
            />
            <FormTextField
              control={form.control}
              name="ogrn"
              label="ОГРН / ОГРНИП"
              rules={{pattern: {value: /^(\d{13}|\d{15})$/, message: "13 или 15 цифр"}}}
              disabled={isPending}
              fullWidth
            />
          </Stack>
          <FormTextField
            control={form.control}
            name="ownershipForm"
            label="Форма собственности"
            placeholder="ООО, ИП…"
            disabled={isPending}
            fullWidth
          />
          {form.formState.errors.root && (
            <Alert severity="error">{form.formState.errors.root.message}</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={onSubmit} disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </>
  );
}

export default OrganizationFormDialog;
