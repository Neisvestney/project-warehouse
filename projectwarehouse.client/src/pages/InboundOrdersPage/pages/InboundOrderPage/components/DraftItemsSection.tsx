import {useEffect, useRef, useState} from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import SaveIcon from "@mui/icons-material/Save";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import {Controller, useFieldArray, useForm, useWatch} from "react-hook-form";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  inboundOrdersGetDraftItemsGroupsOptions,
  inboundOrdersGetDraftItemsGroupsQueryKey,
  inboundOrdersTryAutoAssignCatalogItemsMutation,
  inboundOrdersUpdateDraftItemsGroupsMutation,
} from "@/api/@tanstack/react-query.gen";
import type {AppProblemDetails} from "@/api/types.gen";
import {useHasPermission} from "@/hooks/usePermission";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {useModal} from "@/hooks/useModal";
import {extractErrorMessage, resolveErrorMessage} from "@/utils/errorUtils";
import {CatalogLinkDialog, type CatalogLinkDialogItemInfo} from "./CatalogLinkDialog";

type DraftItemRow = {
  id: string | null;
  name: string;
  article: string;
  barcode: string;
  rootBarcode: string;
  characteristic: string;
  count: string;
  catalogItemId: string | null;
  catalogItemWithCharacteristicId: string | null;
  createNew: boolean;
};

type FormValues = {
  draftItemsGroups: DraftItemRow[];
};

interface Props {
  orderId: string;
  externalErrors?: AppProblemDetails | null;
  onExternalErrorsApplied?: () => void;
  onDirtyChange?: (dirty: boolean) => void;
}

function DraftItemsSection({
  orderId,
  externalErrors,
  onExternalErrorsApplied,
  onDirtyChange,
}: Props) {
  const canEdit = useHasPermission([
    "inbound_orders.edit",
    "inbound_orders.edit_assigned_warehouses",
  ]);
  const queryClient = useQueryClient();
  const {showAlert} = useModal();

  const [selectedIndices, setSelectedIndices] = useState<Set<number>>(new Set());
  const pendingClearRef = useRef(false);
  const [catalogDialog, setCatalogDialog] = useState<{open: boolean; rowIndex: number | null}>({
    open: false,
    rowIndex: null,
  });

  const {data, isLoading} = useQuery(
    inboundOrdersGetDraftItemsGroupsOptions({path: {id: orderId}}),
  );

  const form = useForm<FormValues>({
    defaultValues: {draftItemsGroups: []},
  });
  const {setApiError} = useRhfApiErrors(form);

  const {fields, append, remove} = useFieldArray({
    control: form.control,
    name: "draftItemsGroups",
  });

  const watchedGroups = useWatch({control: form.control, name: "draftItemsGroups"});

  const [prevData, setPrevData] = useState(data);
  if (data !== prevData) {
    setPrevData(data);
    if (data) setSelectedIndices(new Set());
  }

  useEffect(() => {
    if (!data) return;
    pendingClearRef.current = false;
    form.reset({
      draftItemsGroups: data.map((item) => ({
        id: item.id,
        name: item.name,
        article: item.article,
        barcode: item.barcode ?? "",
        rootBarcode: item.rootBarcode ?? "",
        characteristic: item.characteristic,
        count: String(item.count),
        catalogItemId: item.catalogItem?.id ?? null,
        catalogItemWithCharacteristicId: item.catalogItemWithCharacteristic?.id ?? null,
        createNew: item.createNew,
      })),
    });
  }, [data, form]);

  useEffect(() => {
    if (!externalErrors) return;
    for (const [field, errs] of Object.entries(externalErrors.errors)) {
      if (!errs.length) continue;
      const rhfField = field === "root" ? "root" : field.replace(/\[(\d+)\]/g, ".$1");
      form.setError(rhfField as Parameters<typeof form.setError>[0], {
        type: "server",
        message: errs.map(resolveErrorMessage).join(", "),
      });
    }
    pendingClearRef.current = true;
    onExternalErrorsApplied?.();
  }, [externalErrors, form, onExternalErrorsApplied]);

  useEffect(() => {
    if (!pendingClearRef.current) return;
    pendingClearRef.current = false;
    form.clearErrors();
  }, [watchedGroups, form]);

  const saveMutation = useMutation({
    ...inboundOrdersUpdateDraftItemsGroupsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (responseData) => {
      queryClient.setQueryData(
        inboundOrdersGetDraftItemsGroupsQueryKey({path: {id: orderId}}),
        responseData,
      );
    },
    onError: setApiError,
  });

  const autoAssignMutation = useMutation({
    ...inboundOrdersTryAutoAssignCatalogItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (responseData) => {
      const currentValues = form.getValues("draftItemsGroups");
      const byId = new Map(responseData.map((d) => [d.id, d]));
      form.reset({
        draftItemsGroups: currentValues.map((row) => {
          const updated = row.id ? byId.get(row.id) : undefined;
          if (!updated) return row;
          return {
            ...row,
            catalogItemId: updated.catalogItem?.id ?? null,
            catalogItemWithCharacteristicId: updated.catalogItemWithCharacteristic?.id ?? null,
          };
        }),
      });
    },
    onError: (error) =>
      showAlert({title: "Ошибка", message: extractErrorMessage(error), severity: "error"}),
  });

  const onSubmit = form.handleSubmit((values) => {
    saveMutation.mutate({
      path: {id: orderId},
      body: {
        draftItemsGroups: values.draftItemsGroups.map((row) => ({
          id: row.id ?? null,
          name: row.name,
          article: row.article,
          barcode: row.barcode || null,
          rootBarcode: row.rootBarcode || null,
          characteristic: row.characteristic,
          count: Number(row.count) || 1,
          catalogItemId: row.catalogItemId ?? null,
          catalogItemWithCharacteristicId: row.catalogItemWithCharacteristicId ?? null,
          createNew: row.createNew,
        })),
      },
    });
  });

  const handleAutoAssign = () => {
    const currentValues = form.getValues("draftItemsGroups");
    const ids =
      selectedIndices.size > 0
        ? [...selectedIndices].map((i) => currentValues[i]?.id).filter((id): id is string => !!id)
        : currentValues.filter((r) => r.id).map((r) => r.id as string);

    autoAssignMutation.mutate({
      path: {id: orderId},
      body: {draftItemsGroupIds: ids},
    });
  };

  const handleCatalogConfirm = (catalogItemId: string | null, charId: string | null) => {
    const idx = catalogDialog.rowIndex;
    if (idx === null) return;
    form.setValue(`draftItemsGroups.${idx}.catalogItemId`, catalogItemId, {shouldDirty: true});
    form.setValue(`draftItemsGroups.${idx}.catalogItemWithCharacteristicId`, charId, {
      shouldDirty: true,
    });
    setCatalogDialog({open: false, rowIndex: null});
  };

  const allCount = fields.length;
  const allSelected = allCount > 0 && selectedIndices.size === allCount;
  const someSelected = selectedIndices.size > 0 && selectedIndices.size < allCount;

  const toggleSelectAll = () => {
    if (allSelected) {
      setSelectedIndices(new Set());
    } else {
      setSelectedIndices(new Set(fields.map((_, i) => i)));
    }
  };

  const toggleRow = (index: number) => {
    setSelectedIndices((prev) => {
      const next = new Set(prev);
      if (next.has(index)) next.delete(index);
      else next.add(index);
      return next;
    });
  };

  const handleRemove = (index: number) => {
    remove(index);
    setSelectedIndices(new Set());
  };

  const {isDirty} = form.formState;

  useEffect(() => {
    onDirtyChange?.(isDirty);
  }, [isDirty, onDirtyChange]);

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 4}}>
        <CircularProgress />
      </Box>
    );
  }

  const rootError = form.formState.errors.root;
  const isPending = saveMutation.isPending || autoAssignMutation.isPending;

  const dialogRow = catalogDialog.rowIndex !== null ? watchedGroups[catalogDialog.rowIndex] : null;
  const dialogItemInfo: CatalogLinkDialogItemInfo | null = dialogRow
    ? {
        name: dialogRow.name,
        article: dialogRow.article,
        characteristic: dialogRow.characteristic || undefined,
      }
    : null;

  return (
    <Stack spacing={2} component="form" onSubmit={onSubmit}>
      <Stack direction="row" sx={{alignItems: "center", justifyContent: "space-between"}}>
        <Typography variant="h6">Позиции черновика</Typography>
        {canEdit && (
          <Stack direction="row" spacing={1}>
            <Tooltip
              title={
                isDirty
                  ? "Сохраните изменения перед авто-привязкой"
                  : selectedIndices.size > 0
                    ? `Авто-привязка для выбранных (${selectedIndices.size})`
                    : "Авто-привязка для всех"
              }
            >
              <span>
                <Button
                  size="small"
                  startIcon={
                    autoAssignMutation.isPending ? (
                      <CircularProgress size={14} color="inherit" />
                    ) : (
                      <AutoFixHighIcon />
                    )
                  }
                  onClick={handleAutoAssign}
                  disabled={isPending || allCount === 0 || isDirty}
                >
                  Авто-привязка
                </Button>
              </span>
            </Tooltip>
            <Button
              size="small"
              startIcon={<AddIcon />}
              onClick={() =>
                append({
                  id: null,
                  name: "",
                  article: "",
                  barcode: "",
                  rootBarcode: "",
                  characteristic: "",
                  count: "1",
                  catalogItemId: null,
                  catalogItemWithCharacteristicId: null,
                  createNew: false,
                })
              }
              disabled={isPending}
            >
              Добавить
            </Button>
            <Button
              size="small"
              variant="contained"
              startIcon={<SaveIcon />}
              type="submit"
              disabled={isPending || !isDirty}
            >
              {saveMutation.isPending ? (
                <CircularProgress size={16} color="inherit" />
              ) : (
                "Сохранить"
              )}
            </Button>
          </Stack>
        )}
      </Stack>

      {rootError && <Alert severity="error">{rootError.message}</Alert>}

      <Paper>
        <TableContainer sx={{overflowX: "auto"}}>
          <Table size="small" sx={{minWidth: 1200}}>
            <TableHead>
              <TableRow>
                {canEdit && (
                  <TableCell sx={{textAlign: "center", pt: "6px !important"}}>
                    <Checkbox
                      size="small"
                      checked={allSelected}
                      indeterminate={someSelected}
                      onChange={toggleSelectAll}
                      disabled={allCount === 0}
                    />
                  </TableCell>
                )}
                <TableCell sx={{width: 36}}>#</TableCell>
                <TableCell sx={{minWidth: 180}}>Название</TableCell>
                <TableCell sx={{minWidth: 130}}>Артикул</TableCell>
                <TableCell sx={{minWidth: 130}}>Штрихкод товара</TableCell>
                <TableCell sx={{minWidth: 130}}>Штрихкод товара с характеристикой</TableCell>
                <TableCell sx={{minWidth: 160}}>Характеристика</TableCell>
                <TableCell sx={{width: 150}}>Кол-во</TableCell>
                <TableCell sx={{minWidth: 120}}>Каталог</TableCell>
                <TableCell sx={{width: 70, textAlign: "center"}}>Новый</TableCell>
                {canEdit && <TableCell sx={{width: 50}} />}
              </TableRow>
            </TableHead>
            <TableBody>
              {fields.length === 0 && (
                <TableRow>
                  <TableCell colSpan={canEdit ? 11 : 9} align="center" sx={{py: 3}}>
                    <Typography variant="body2" color="text.secondary">
                      {canEdit
                        ? "Нет позиций. Нажмите «Добавить», чтобы создать первую."
                        : "Нет позиций."}
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
              {fields.map((field, index) => {
                const rowErrors = form.formState.errors.draftItemsGroups?.[index];
                const watched = watchedGroups[index];
                const isLinked = !!watched?.catalogItemWithCharacteristicId;
                const hasCatalogItemOnly =
                  !!watched?.catalogItemId && !watched?.catalogItemWithCharacteristicId;
                const willCreate = !!watched?.createNew;
                const linkError = rowErrors?.catalogItemWithCharacteristicId?.message;

                return (
                  <TableRow
                    key={field.id}
                    selected={selectedIndices.has(index)}
                    sx={{
                      "& .MuiTableCell-root": {verticalAlign: "top", pt: 1, pb: 0.5},
                      bgcolor: linkError ? "rgba(211,47,47,0.08)" : undefined,
                    }}
                  >
                    {canEdit && (
                      <TableCell sx={{textAlign: "center", pt: "6px !important"}}>
                        <Checkbox
                          size="small"
                          checked={selectedIndices.has(index)}
                          onChange={() => toggleRow(index)}
                        />
                      </TableCell>
                    )}

                    <TableCell sx={{pt: "18px !important"}}>
                      <Typography variant="body2" color="text.secondary">
                        {index + 1}
                      </Typography>
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.name`}
                        rules={{required: "Обязательное поле"}}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.article`}
                        rules={{required: "Обязательное поле"}}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.rootBarcode`}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.barcode`}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.characteristic`}
                        rules={{required: "Обязательное поле"}}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            size="small"
                            fullWidth
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.count`}
                        rules={{
                          validate: (v) =>
                            (Number(v) >= 1 && Number.isInteger(Number(v))) || "Минимум 1",
                        }}
                        render={({field: f, fieldState}) => (
                          <TextField
                            {...f}
                            type="number"
                            size="small"
                            fullWidth
                            slotProps={{htmlInput: {min: 1}}}
                            error={!!fieldState.error}
                            helperText={fieldState.error?.message}
                            disabled={!canEdit || isPending}
                            onChange={(e) => f.onChange(e.target.value)}
                          />
                        )}
                      />
                    </TableCell>

                    <TableCell sx={{pt: "12px !important"}}>
                      {(() => {
                        const chipProps = canEdit
                          ? {
                              onClick: () => setCatalogDialog({open: true, rowIndex: index}),
                              sx: {cursor: "pointer"},
                            }
                          : {};
                        if (isLinked) {
                          return (
                            <Chip label="Привязан" size="small" color="success" {...chipProps} />
                          );
                        }
                        if (hasCatalogItemOnly && willCreate) {
                          return (
                            <Tooltip title="Товар выбран, характеристика будет создана">
                              <Chip
                                label="Создать хар-ку"
                                size="small"
                                color="info"
                                {...chipProps}
                              />
                            </Tooltip>
                          );
                        }
                        if (hasCatalogItemOnly) {
                          return (
                            <Tooltip title="Товар выбран, характеристика не указана">
                              <Chip
                                label="Нет хар-ки"
                                size="small"
                                color="secondary"
                                {...chipProps}
                              />
                            </Tooltip>
                          );
                        }
                        if (willCreate) {
                          return <Chip label="Создать" size="small" color="info" {...chipProps} />;
                        }
                        return (
                          <Tooltip title="Нажмите, чтобы привязать к каталогу">
                            <Chip label="Не привязан" size="small" color="warning" {...chipProps} />
                          </Tooltip>
                        );
                      })()}
                    </TableCell>

                    <TableCell sx={{textAlign: "center", pt: "6px !important"}}>
                      <Controller
                        control={form.control}
                        name={`draftItemsGroups.${index}.createNew`}
                        render={({field: f}) => (
                          <Checkbox
                            checked={f.value}
                            onChange={(e) => f.onChange(e.target.checked)}
                            size="small"
                            disabled={!canEdit || isPending || isLinked}
                          />
                        )}
                      />
                    </TableCell>

                    {canEdit && (
                      <TableCell sx={{pt: "6px !important"}}>
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => handleRemove(index)}
                          disabled={isPending}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>

      <CatalogLinkDialog
        open={catalogDialog.open}
        onClose={() => setCatalogDialog({open: false, rowIndex: null})}
        onConfirm={handleCatalogConfirm}
        initialCatalogItemId={dialogRow?.catalogItemId ?? null}
        initialCharacteristicId={dialogRow?.catalogItemWithCharacteristicId ?? null}
        itemInfo={dialogItemInfo}
      />
    </Stack>
  );
}

export default DraftItemsSection;
