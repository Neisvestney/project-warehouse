import {useEffect, useState} from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import EditIcon from "@mui/icons-material/Edit";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import {useFieldArray, useForm} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  catalogGetAllQueryKey,
  catalogGetByIdOptions,
  catalogGetByIdQueryKey,
  catalogUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {FormTextField} from "@/components/form/FormTextField";

const DRAWER_WIDTH = 1000;

type CharacteristicField = {
  id?: string;
  characteristic: string;
  barcode: string;
};

type CatalogFormValues = {
  name: string;
  article: string;
  barcode: string;
  characteristics: CharacteristicField[];
};

function ViewMode({
  itemId,
  onEdit,
  canEdit,
}: {
  itemId: string;
  onEdit: () => void;
  canEdit: boolean;
}) {
  const {data, isLoading} = useQuery({
    ...catalogGetByIdOptions({path: {id: itemId}}),
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 4}}>
        <CircularProgress />
      </Box>
    );
  }

  if (!data) return null;

  return (
    <Box sx={{overflowY: "auto", px: 2, py: 2, flex: 1}}>
      <Stack spacing={1.5}>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <Typography variant="body2" color="text.secondary" sx={{minWidth: 100}}>
            Артикул
          </Typography>
          <Typography variant="body2">{data.article}</Typography>
        </Stack>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <Typography variant="body2" color="text.secondary" sx={{minWidth: 100}}>
            Штрихкод
          </Typography>
          <Typography variant="body2">{data.barcode ?? "—"}</Typography>
        </Stack>

        <Divider />

        <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <Typography variant="subtitle2">Характеристики</Typography>
            {data.characteristics.length > 0 && (
              <Chip label={data.characteristics.length} size="small" />
            )}
          </Stack>
        </Stack>

        {data.characteristics.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            Нет характеристик
          </Typography>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Характеристика</TableCell>
                <TableCell>Штрихкод</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.characteristics.map((c) => (
                <TableRow key={c.id}>
                  <TableCell>{c.characteristic}</TableCell>
                  <TableCell>{c.barcode ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
        {canEdit && (
          <Box>
            <Button size="small" startIcon={<EditIcon />} onClick={onEdit}>
              Редактировать
            </Button>
          </Box>
        )}
      </Stack>
    </Box>
  );
}

function EditMode({itemId, onClose}: {itemId: string; onClose: () => void}) {
  const queryClient = useQueryClient();

  const {data} = useQuery({
    ...catalogGetByIdOptions({path: {id: itemId}}),
  });

  const form = useForm<CatalogFormValues>({
    defaultValues: {name: "", article: "", barcode: "", characteristics: []},
  });
  const {setApiError} = useRhfApiErrors(form);
  const {reset} = form;

  const {fields, append, remove} = useFieldArray({
    control: form.control,
    name: "characteristics",
  });

  useEffect(() => {
    if (!data) return;
    reset({
      name: data.name,
      article: data.article,
      barcode: data.barcode ?? "",
      characteristics: data.characteristics.map((c) => ({
        id: c.id,
        characteristic: c.characteristic,
        barcode: c.barcode ?? "",
      })),
    });
  }, [data, reset]);

  const mutation = useMutation({
    ...catalogUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: catalogGetByIdQueryKey({path: {id: itemId}})});
      await queryClient.invalidateQueries({queryKey: catalogGetAllQueryKey()});
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({
      path: {id: itemId},
      body: {
        name: values.name,
        article: values.article,
        barcode: values.barcode || null,
        characteristics: values.characteristics.map((c) => ({
          id: c.id ?? null,
          characteristic: c.characteristic,
          barcode: c.barcode || null,
        })),
      },
    });
  });

  return (
    <Box
      component="form"
      onSubmit={onSubmit}
      sx={{overflowY: "auto", px: 2, py: 2, flex: 1, display: "flex", flexDirection: "column"}}
    >
      <Stack spacing={2} sx={{flex: 1}}>
        <FormTextField
          control={form.control}
          name="name"
          label="Название"
          size="small"
          fullWidth
          disabled={mutation.isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={form.control}
          name="article"
          label="Артикул"
          size="small"
          fullWidth
          disabled={mutation.isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={form.control}
          name="barcode"
          label="Штрихкод"
          size="small"
          fullWidth
          disabled={mutation.isPending}
        />

        <Divider />

        <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
          <Typography variant="subtitle2">Характеристики</Typography>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => append({characteristic: "", barcode: ""})}
            disabled={mutation.isPending}
          >
            Добавить
          </Button>
        </Stack>

        {fields.length === 0 && (
          <Typography variant="body2" color="text.secondary">
            Нет характеристик
          </Typography>
        )}

        {fields.map((field, index) => (
          <Stack key={field.id} direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
            <FormTextField
              control={form.control}
              name={`characteristics.${index}.characteristic`}
              label="Характеристика"
              size="small"
              fullWidth
              disabled={mutation.isPending}
              rules={{required: "Обязательное поле"}}
            />
            <FormTextField
              control={form.control}
              name={`characteristics.${index}.barcode`}
              label="Штрихкод"
              size="small"
              fullWidth
              disabled={mutation.isPending}
            />
            <IconButton
              size="small"
              onClick={() => remove(index)}
              disabled={mutation.isPending}
              sx={{mt: 0.5}}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Stack>
        ))}

        {form.formState.errors.root && (
          <Alert severity="error">{form.formState.errors.root.message}</Alert>
        )}
      </Stack>

      <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end", pt: 2}}>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Отмена
        </Button>
        <Button type="submit" variant="contained" disabled={mutation.isPending}>
          {mutation.isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </Stack>
    </Box>
  );
}

export interface CatalogItemDrawerProps {
  itemId: string | null;
  onClose: () => void;
}

export function CatalogItemDrawer({itemId, onClose}: CatalogItemDrawerProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [prevItemId, setPrevItemId] = useState(itemId);
  const canEdit = useHasPermission("catalog.edit");

  if (prevItemId !== itemId) {
    setPrevItemId(itemId);
    setIsEditing(false);
  }

  const {data} = useQuery({
    ...catalogGetByIdOptions({path: {id: itemId!}}),
    enabled: !!itemId,
  });

  const handleClose = () => {
    setIsEditing(false);
    onClose();
  };

  return (
    <Drawer
      anchor="right"
      open={!!itemId}
      onClose={handleClose}
      slotProps={{
        paper: {
          sx: {
            width: DRAWER_WIDTH,
            display: "flex",
            flexDirection: "column",
            maxWidth: "calc(100vw - 10px)",
          },
        },
      }}
    >
      <Stack
        direction="row"
        sx={{alignItems: "center", justifyContent: "space-between", px: 2, py: 1.5, flexShrink: 0}}
      >
        <Typography variant="h6" noWrap sx={{flex: 1, mr: 1}}>
          {data?.name ?? ""}
        </Typography>
        <IconButton onClick={handleClose} size="small">
          <CloseIcon />
        </IconButton>
      </Stack>
      <Divider />

      {itemId && !isEditing && (
        <ViewMode itemId={itemId} canEdit={canEdit} onEdit={() => setIsEditing(true)} />
      )}
      {itemId && isEditing && <EditMode itemId={itemId} onClose={() => setIsEditing(false)} />}
    </Drawer>
  );
}
