import {useEffect} from "react";
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
import {useMutation} from "@tanstack/react-query";
import {tagsCreateMutation, tagsRenameMutation} from "@/api/@tanstack/react-query.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";
import {TAG_KIND_LABELS} from "./tagKinds";
import type {TagDto, TagKind} from "@/api/types.gen";

interface Values {
  name: string;
}

interface TagDialogProps {
  open: boolean;
  /** null means create a tag of `kind`. */
  tag: TagDto | null;
  kind: TagKind;
  onClose: () => void;
  onSaved: () => Promise<void> | void;
}

function TagDialog({open, tag, kind, onClose, onSaved}: TagDialogProps) {
  const form = useForm<Values>({defaultValues: {name: ""}});
  const {control, handleSubmit, reset, formState} = form;
  const {setApiError} = useRhfApiErrors(form);

  useEffect(() => {
    if (open) reset({name: tag?.name ?? ""});
  }, [open, tag, reset]);

  const onSuccess = async () => {
    await onSaved();
    onClose();
  };

  const create = useMutation({
    ...tagsCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess,
    onError: setApiError,
  });

  const rename = useMutation({
    ...tagsRenameMutation(),
    meta: {suppressGlobalError: true},
    onSuccess,
    onError: setApiError,
  });

  const isPending = create.isPending || rename.isPending;

  const submit = handleSubmit(({name}) => {
    if (tag) rename.mutate({path: {id: tag.id}, body: {name}});
    else create.mutate({body: {kind, name}});
  });

  useBackClosable(open && !isPending, onClose);

  return (
    <Dialog open={open} onClose={isPending ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>
        {tag ? "Переименовать тег" : `Новый тег · ${TAG_KIND_LABELS[kind]}`}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
          <FormTextField
            control={control}
            name="name"
            label="Название"
            size="small"
            fullWidth
            autoFocus
            rules={{
              required: "Укажите название",
              maxLength: {value: 100, message: "Не больше 100 символов"},
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter") void submit();
            }}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={submit} disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default TagDialog;
