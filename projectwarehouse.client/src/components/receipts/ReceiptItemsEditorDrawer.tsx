import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Drawer,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import CloseIcon from "@mui/icons-material/Close";
import {Controller, useFieldArray, useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {receiptsSyncItemsMutation} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import type {ReceiptDto} from "@/api/types.gen";

interface ItemRow {
  catalogItemId: string | null;
  plannedCount: number;
  notes: string;
}

interface ReceiptItemsEditorDrawerProps {
  open: boolean;
  onClose: () => void;
  receipt: ReceiptDto;
  onUpdate: (updated: ReceiptDto) => void;
}

function ReceiptItemsEditorDrawer({
  open,
  onClose,
  receipt,
  onUpdate,
}: ReceiptItemsEditorDrawerProps) {
  const form = useForm<{items: ItemRow[]}>({
    defaultValues: {
      items: receipt.items.map((item) => ({
        catalogItemId: item.catalogItemId,
        plannedCount: item.plannedCount,
        notes: item.notes ?? "",
      })),
    },
  });

  const {fields, append, remove} = useFieldArray({
    control: form.control,
    name: "items",
  });

  const {setApiError} = useRhfApiErrors(form);

  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("lg"));

  const mutation = useMutation({
    ...receiptsSyncItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => {
      onUpdate(data);
      onClose();
    },
    onError: setApiError,
  });

  const onSubmit = form.handleSubmit((values) => {
    const hasEmpty = values.items.some((i) => i.catalogItemId === null);
    if (hasEmpty) {
      form.setError("root", {message: "Заполните товар во всех строках или удалите пустые"});
      return;
    }
    const ids = values.items.map((i) => i.catalogItemId);
    const unique = new Set(ids);
    if (ids.length !== unique.size) {
      form.setError("root", {message: "Один и тот же товар добавлен несколько раз"});
      return;
    }
    const typedItems = values.items as (ItemRow & {catalogItemId: string})[];
    mutation.mutate({
      path: {id: receipt.id},
      body: typedItems.map((i) => ({
        catalogItemId: i.catalogItemId,
        plannedCount: i.plannedCount,
        notes: i.notes || null,
      })),
    });
  });

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={mutation.isPending ? undefined : onClose}
      slotProps={{
        paper: {
          sx: {maxWidth: "100vw", minWidth: "calc(min(1200px, 100vw))"},
        },
      }}
    >
      <Box sx={{display: "flex", flexDirection: "column", height: "100%"}}>
        <Stack direction="row" sx={{alignItems: "center", px: 2, pt: 2, pb: 1, flexShrink: 0}}>
          <Typography variant="h6" sx={{flexGrow: 1}}>
            Позиции приемки
          </Typography>
          <IconButton onClick={onClose} disabled={mutation.isPending}>
            <CloseIcon />
          </IconButton>
        </Stack>

        <Box component="form" onSubmit={onSubmit} sx={{overflow: "auto", flexGrow: 1, px: 2}}>
          {isMobile ? (
            <Stack spacing={1} sx={{py: 1}}>
              {fields.map((field, index) => (
                <Paper key={field.id} variant="outlined" sx={{p: 1.5}}>
                  <Stack spacing={1}>
                    <Controller
                      control={form.control}
                      name={`items.${index}.catalogItemId`}
                      rules={{required: true}}
                      render={({field: f, fieldState}) => (
                        <CatalogItemsSelect
                          value={f.value}
                          onChange={f.onChange}
                          types={["standard", "unit", "assembledBundle"]}
                          size="small"
                          disabled={mutation.isPending}
                          textFieldProps={{
                            error: !!fieldState.error,
                            placeholder: "Выберите товар",
                          }}
                          sx={{width: "100%"}}
                        />
                      )}
                    />
                    <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
                      <Controller
                        control={form.control}
                        name={`items.${index}.plannedCount`}
                        rules={{required: true, min: 1}}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            type="number"
                            size="small"
                            label="Кол-во"
                            error={!!fieldState.error}
                            disabled={mutation.isPending}
                            slotProps={{htmlInput: {min: 1}}}
                            onChange={(e) => f.onChange(Number(e.target.value))}
                            sx={{width: 100}}
                          />
                        )}
                      />
                      <Controller
                        control={form.control}
                        name={`items.${index}.notes`}
                        render={({field: f}) => (
                          <TextField
                            {...f}
                            size="small"
                            label="Примечание"
                            placeholder="Необязательно"
                            disabled={mutation.isPending}
                            sx={{flexGrow: 1}}
                          />
                        )}
                      />
                      <IconButton
                        size="small"
                        onClick={() => remove(index)}
                        disabled={mutation.isPending}
                        color="error"
                        sx={{mt: 0.5}}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  </Stack>
                </Paper>
              ))}
              {fields.length === 0 && (
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{py: 2, textAlign: "center"}}
                >
                  Нет позиций — добавьте товары
                </Typography>
              )}
            </Stack>
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Товар</TableCell>
                  <TableCell sx={{width: 100}}>Кол-во</TableCell>
                  <TableCell>Примечание</TableCell>
                  <TableCell sx={{width: 48}} />
                </TableRow>
              </TableHead>
              <TableBody>
                {fields.map((field, index) => (
                  <TableRow key={field.id}>
                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`items.${index}.catalogItemId`}
                        rules={{required: true}}
                        render={({field: f, fieldState}) => (
                          <CatalogItemsSelect
                            value={f.value}
                            onChange={f.onChange}
                            types={["standard", "unit", "assembledBundle"]}
                            size="small"
                            disabled={mutation.isPending}
                            textFieldProps={{
                              error: !!fieldState.error,
                              placeholder: "Выберите товар",
                            }}
                            sx={{minWidth: 200}}
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`items.${index}.plannedCount`}
                        rules={{required: true, min: 1}}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            type="number"
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            disabled={mutation.isPending}
                            slotProps={{htmlInput: {min: 1}}}
                            onChange={(e) => f.onChange(Number(e.target.value))}
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`items.${index}.notes`}
                        render={({field: f}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            placeholder="Необязательно"
                            disabled={mutation.isPending}
                          />
                        )}
                      />
                    </TableCell>
                    <TableCell>
                      <IconButton
                        size="small"
                        onClick={() => remove(index)}
                        disabled={mutation.isPending}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
                {fields.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={4} align="center" sx={{py: 3, color: "text.secondary"}}>
                      Нет позиций — добавьте товары
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          )}

          {form.formState.errors.root && (
            <Alert severity="error" sx={{mt: 1}}>
              {form.formState.errors.root.message}
            </Alert>
          )}
        </Box>

        <Stack
          direction="row"
          spacing={1}
          sx={{px: 2, py: 2, flexShrink: 0, borderTop: 1, borderColor: "divider"}}
        >
          <Button
            startIcon={<AddIcon />}
            onClick={() => append({catalogItemId: null, plannedCount: 1, notes: ""})}
            disabled={mutation.isPending}
          >
            Добавить позицию
          </Button>
          <Box sx={{flexGrow: 1}} />
          <Button onClick={onClose} disabled={mutation.isPending}>
            Отмена
          </Button>
          <Button variant="contained" onClick={onSubmit} disabled={mutation.isPending}>
            {mutation.isPending ? <CircularProgress size={22} color="inherit" /> : "Сохранить"}
          </Button>
        </Stack>
      </Box>
    </Drawer>
  );
}

export default ReceiptItemsEditorDrawer;
