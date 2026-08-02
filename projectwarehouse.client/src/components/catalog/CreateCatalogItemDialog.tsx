import {Controller, useForm} from "react-hook-form";
import {useMutation, useQueryClient} from "@tanstack/react-query";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
} from "@mui/material";
import {catalogCreateMutation, catalogGetAllQueryKey} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemType} from "@/api/types.gen";
import {CATALOG_ITEM_TYPE_CONFIG} from "@/features/catalog";
import {FormTextField} from "@/components/form/FormTextField";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";

type CreateFormValues = {
  type: CatalogItemType;
  name: string;
  article: string;
  barcode: string;
};

const CREATABLE_TYPES: CatalogItemType[] = [
  "standard",
  "unit",
  "productGroup",
  "variation",
  "bundle",
];

interface CreateCatalogItemDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (id: string) => void;
}

export function CreateCatalogItemDialog({open, onClose, onCreated}: CreateCatalogItemDialogProps) {
  const queryClient = useQueryClient();

  const form = useForm<CreateFormValues>({
    defaultValues: {type: "standard", name: "", article: "", barcode: ""},
  });
  const {setApiError} = useRhfApiErrors(form);
  const {control, formState, reset} = form;

  const mutation = useMutation({
    ...catalogCreateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async (data) => {
      await queryClient.invalidateQueries({queryKey: catalogGetAllQueryKey()});
      reset();
      onCreated(data.id);
    },
    onError: setApiError,
  });

  const isPending = mutation.isPending;

  const handleClose = () => {
    if (isPending) return;
    reset();
    onClose();
  };

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      body: {
        type: values.type,
        name: values.name,
        article: values.article,
        barcode: values.barcode || null,
      },
    });
  });

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="xs" fullWidth>
      <DialogTitle>Создать позицию каталога</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <Controller
            control={control}
            name="type"
            render={({field, fieldState}) => (
              <FormControl size="small" fullWidth error={!!fieldState.error}>
                <InputLabel>Тип</InputLabel>
                <Select {...field} label="Тип" disabled={isPending}>
                  {CREATABLE_TYPES.map((type) => (
                    <MenuItem key={type} value={type}>
                      {CATALOG_ITEM_TYPE_CONFIG[type].label}
                    </MenuItem>
                  ))}
                </Select>
                {fieldState.error && <FormHelperText>{fieldState.error.message}</FormHelperText>}
              </FormControl>
            )}
          />
          <FormTextField
            control={control}
            name="name"
            label="Название"
            size="small"
            fullWidth
            disabled={isPending}
            rules={{required: "Обязательное поле"}}
          />
          <FormTextField
            control={control}
            name="article"
            label="Артикул"
            size="small"
            fullWidth
            disabled={isPending}
            rules={{required: "Обязательное поле"}}
          />
          <FormTextField
            control={control}
            name="barcode"
            label="Штрихкод"
            size="small"
            fullWidth
            disabled={isPending}
          />
          {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={isPending}>
          Отмена
        </Button>
        <Button onClick={onSubmit} variant="contained" disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Создать"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
