import React, {useEffect, useMemo, useState} from "react";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useFieldArray, useForm} from "react-hook-form";
import {
  Alert,
  Autocomplete,
  Button,
  FormHelperText,
  IconButton,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import {
  catalogGetAllOptions,
  catalogGetByIdOptions,
  inboundOrderProcessingGetByIdQueryKey,
  inboundOrderProcessingGetStoragePlaceNodeDetailsQueryKey,
  inboundOrderProcessingPlaceItemsMutation,
  inboundOrderProcessingUpdateItemsMutation,
} from "@/api/@tanstack/react-query.gen.ts";
import {type CatalogItemSummaryDto, type ItemsGroupDto} from "@/api/types.gen.ts";
import {useDebounce} from "@/hooks/useDebounce.ts";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors.ts";

interface EditItemRow {
  catalogItemSummary: CatalogItemSummaryDto | null;
  catalogItemWithCharacteristicId: string | null;
  count: number;
}

interface EditItemsFormValues {
  items: EditItemRow[];
}

interface EditItemRowProps {
  index: number;
  row: EditItemRow;
  onChange: (updated: EditItemRow) => void;
  onDelete: () => void;
  disabled?: boolean;
  errorMessage?: string;
}

function EditItemRowComponent({
  index,
  row,
  onChange,
  onDelete,
  disabled,
  errorMessage,
}: EditItemRowProps) {
  const [searchString, setSearchString] = useState("");
  const debouncedSearch = useDebounce(searchString, 300);

  const catalogQuery = useQuery(
    catalogGetAllOptions({query: {searchString: debouncedSearch || undefined, pageSize: 20}}),
  );

  const catalogItemId = row.catalogItemSummary?.id;
  const catalogItemQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: catalogItemId!}}),
    enabled: !!catalogItemId,
  });

  const options = useMemo<CatalogItemSummaryDto[]>(() => {
    const results = catalogQuery.data?.items ?? [];
    const seen = new Set(results.map((r) => r.id));
    const current = row.catalogItemSummary;
    if (current && !seen.has(current.id)) {
      return [...results, current];
    }
    return results;
  }, [catalogQuery.data, row.catalogItemSummary]);

  const characteristics = catalogItemQuery.data?.characteristics ?? [];

  return (
    <>
      <TableRow sx={errorMessage ? {"& > *": {borderBottom: "none"}} : {}}>
        <TableCell sx={{minWidth: 220}}>
          <Autocomplete
            size="small"
            options={options}
            value={row.catalogItemSummary ?? null}
            onChange={(_, dto) =>
              onChange({
                ...row,
                catalogItemSummary: dto,
                catalogItemWithCharacteristicId: null,
              })
            }
            onInputChange={(_, v, reason) => {
              if (reason !== "reset") setSearchString(v);
            }}
            getOptionLabel={(r) => `${r.name} (${r.article})`}
            isOptionEqualToValue={(o, v) => o.id === v.id}
            filterOptions={(x) => x}
            loading={catalogQuery.isLoading}
            disabled={disabled}
            renderInput={(params) => <TextField {...params} label="Товар" />}
          />
        </TableCell>
        <TableCell sx={{minWidth: 160}}>
          <Select
            size="small"
            fullWidth
            displayEmpty
            value={row.catalogItemWithCharacteristicId ?? ""}
            onChange={(e) =>
              onChange({...row, catalogItemWithCharacteristicId: e.target.value || null})
            }
            disabled={disabled || !catalogItemId || catalogItemQuery.isLoading}
          >
            <MenuItem value="">
              <em>—</em>
            </MenuItem>
            {characteristics.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.characteristic || "—"}
              </MenuItem>
            ))}
          </Select>
        </TableCell>
        <TableCell align="right" sx={{width: 90}}>
          <TextField
            size="small"
            type="number"
            value={row.count}
            onChange={(e) => onChange({...row, count: Math.max(1, Number(e.target.value))})}
            slotProps={{htmlInput: {min: 1}}}
            sx={{width: 80}}
            disabled={disabled}
          />
        </TableCell>
        <TableCell padding="checkbox">
          <IconButton size="small" onClick={onDelete} color="error" disabled={disabled}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </TableCell>
      </TableRow>
      {errorMessage && (
        <TableRow>
          <TableCell colSpan={4}>
            <FormHelperText error>{errorMessage}</FormHelperText>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

interface EditItemsTableProps {
  orderId: string;
  nodeId: string;
  mode: "place" | "update";
  initialItems: ItemsGroupDto[];
  onSuccess: () => void;
  onCancel: () => void;
}

function EditItemsTable({
  orderId,
  nodeId,
  mode,
  initialItems,
  onSuccess,
  onCancel,
}: EditItemsTableProps) {
  const queryClient = useQueryClient();

  const form = useForm<EditItemsFormValues>({defaultValues: {items: []}});
  const {fields, append, remove, update} = useFieldArray({control: form.control, name: "items"});
  const {setApiError} = useRhfApiErrors(form);
  useEffect(() => {
    form.reset({
      items: initialItems.map((g) => ({
        catalogItemSummary: {
          id: g.catalogItemWithCharacteristic.catalogItem.id,
          name: g.catalogItemWithCharacteristic.catalogItem.name,
          article: g.catalogItemWithCharacteristic.catalogItem.article,
          characteristicCount: 0,
        },
        catalogItemWithCharacteristicId: g.catalogItemWithCharacteristic.id,
        count: g.count,
      })),
    });
    // mount-only init: parent remounts this component on mode switch, re-running would reset edits in progress
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSuccess = () => {
    void queryClient.invalidateQueries({
      queryKey: inboundOrderProcessingGetStoragePlaceNodeDetailsQueryKey({
        path: {id: orderId, nodeId},
      }),
    });
    void queryClient.invalidateQueries({
      queryKey: inboundOrderProcessingGetByIdQueryKey({path: {id: orderId}}),
    });
    onSuccess();
  };

  const placeMutation = useMutation({
    ...inboundOrderProcessingPlaceItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: handleSuccess,
    onError: setApiError,
  });

  const updateMutation = useMutation({
    ...inboundOrderProcessingUpdateItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: handleSuccess,
    onError: setApiError,
  });

  const isPending = placeMutation.isPending || updateMutation.isPending;

  const onSave = (data: EditItemsFormValues) => {
    let hasErrors = false;
    data.items.forEach((r, i) => {
      if (!r.catalogItemWithCharacteristicId) {
        form.setError(`items.${i}.catalogItemWithCharacteristicId`, {
          type: "required",
          message: "Выберите характеристику",
        });
        hasErrors = true;
      }
    });
    if (hasErrors) return;

    const body = {
      items: data.items.map((r) => ({
        catalogItemWithCharacteristicId: r.catalogItemWithCharacteristicId!,
        count: r.count,
      })),
    };
    if (mode === "place") {
      placeMutation.mutate({path: {id: orderId, nodeId}, body});
    } else {
      updateMutation.mutate({path: {id: orderId, nodeId}, body});
    }
  };

  const handleSave = () => {
    form.clearErrors();
    void form.handleSubmit(onSave)();
  };

  return (
    <Stack spacing={1.5}>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Товар</TableCell>
              <TableCell>Характеристика</TableCell>
              <TableCell align="right">Кол-во</TableCell>
              <TableCell padding="checkbox" />
            </TableRow>
          </TableHead>
          <TableBody>
            {fields.map((field, i) => (
              <EditItemRowComponent
                key={field.id}
                index={i}
                row={fields[i] as EditItemRow}
                onChange={(updated) => update(i, updated)}
                onDelete={() => remove(i)}
                disabled={isPending}
                errorMessage={
                  form.formState.errors?.items?.[i]?.catalogItemWithCharacteristicId?.message
                }
              />
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Button
        size="small"
        startIcon={<AddIcon />}
        onClick={() =>
          append({catalogItemSummary: null, catalogItemWithCharacteristicId: null, count: 1})
        }
        disabled={isPending}
        sx={{alignSelf: "flex-start"}}
      >
        Добавить товар
      </Button>

      {form.formState.errors.root && (
        <Alert severity="error">{form.formState.errors.root.message}</Alert>
      )}

      <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
        <Button size="small" onClick={onCancel} disabled={isPending}>
          Отмена
        </Button>
        <Button
          size="small"
          variant="contained"
          onClick={handleSave}
          disabled={isPending}
          loading={isPending}
        >
          Сохранить
        </Button>
      </Stack>
    </Stack>
  );
}

export default EditItemsTable;
