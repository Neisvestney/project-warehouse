import React, {useMemo, useState} from "react";
import {useMutation, useQuery, useQueryClient} from "@tanstack/react-query";
import {useForm} from "react-hook-form";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormHelperText,
  IconButton,
  MenuItem,
  Select,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import {
  catalogGetAllOptions,
  catalogGetByIdOptions,
  storagePlacesGetNodeDetailsOptions,
  storagePlacesGetNodeDetailsQueryKey,
  storagePlacesGetNodesQueryKey,
  storagePlacesUpdateNodeItemsMutation,
  warehousesGetByIdQueryKey,
} from "@/api/@tanstack/react-query.gen.ts";
import {type CatalogItemSummaryDto} from "@/api";
import {useDebounce} from "@/hooks/useDebounce.ts";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors.ts";

interface EditRow {
  key: string;
  serverGroupId?: string;
  catalogItemSummary: CatalogItemSummaryDto | null;
  catalogItemWithCharacteristicId: string | null;
  count: number;
}

interface EditItemRowProps {
  row: EditRow;
  onChange: (updated: EditRow) => void;
  onDelete: () => void;
  disabled?: boolean;
  errorMessage?: string;
}

function EditItemRow({row, onChange, onDelete, disabled, errorMessage}: EditItemRowProps) {
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
          <TableCell colSpan={3}>
            <FormHelperText error>{errorMessage}</FormHelperText>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

interface NodeDetailsProps {
  storagePlaceId: string;
  warehouseId: string;
  nodeId: string;
}

function NodeDetails({storagePlaceId, warehouseId, nodeId}: NodeDetailsProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [editRows, setEditRows] = useState<EditRow[]>([]);

  const form = useForm<{catalogItemWithCharacteristicId: string}[]>();
  const {setApiError} = useRhfApiErrors(form);

  const {data, isLoading} = useQuery(
    storagePlacesGetNodeDetailsOptions({path: {id: storagePlaceId, nodeId}}),
  );

  const updateMutation = useMutation({
    ...storagePlacesUpdateNodeItemsMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (result) => {
      queryClient.setQueryData(
        storagePlacesGetNodeDetailsQueryKey({path: {id: storagePlaceId, nodeId}}),
        result,
      );
      void queryClient.invalidateQueries({
        queryKey: storagePlacesGetNodesQueryKey({path: {id: storagePlaceId}}),
      });
      void queryClient.invalidateQueries({
        queryKey: warehousesGetByIdQueryKey({path: {id: warehouseId}}),
      });
      setIsEditing(false);
    },
    onError: setApiError,
  });

  const enterEditMode = () => {
    form.clearErrors();
    setEditRows(
      (data?.itemsGroups ?? []).map((g) => ({
        key: g.id,
        serverGroupId: g.id,
        catalogItemSummary: {
          id: g.catalogItemWithCharacteristic.catalogItem.id,
          name: g.catalogItemWithCharacteristic.catalogItem.name,
          article: g.catalogItemWithCharacteristic.catalogItem.article,
          characteristicCount: 0,
        },
        catalogItemWithCharacteristicId: g.catalogItemWithCharacteristic.id,
        count: g.count,
      })),
    );
    setIsEditing(true);
  };

  const cancelEdit = () => {
    setIsEditing(false);
    setEditRows([]);
    form.clearErrors();
  };

  const addRow = () => {
    setEditRows((prev) => [
      ...prev,
      {
        key: crypto.randomUUID(),
        catalogItemSummary: null,
        catalogItemWithCharacteristicId: null,
        count: 1,
      },
    ]);
  };

  const updateRow = (key: string, updated: EditRow) => {
    const index = editRows.findIndex((x) => x.key === key);
    form.clearErrors(`${index}`);
    setEditRows((prev) => prev.map((r) => (r.key === key ? updated : r)));
  };

  const removeRow = (key: string) => {
    setEditRows((prev) => prev.filter((r) => r.key !== key));
  };

  const save = () => {
    form.clearErrors();
    updateMutation.mutate({
      path: {id: storagePlaceId, nodeId},
      body: editRows.map((r) => ({
        id: r.serverGroupId ?? null,
        catalogItemWithCharacteristicId: r.catalogItemWithCharacteristicId!,
        count: r.count,
      })),
    });
  };

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", py: 2}}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (!data) return null;

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" spacing={1} sx={{alignItems: "center", height: 31}}>
        <Typography variant="subtitle2">Содержимое ячейки</Typography>
        {/*{!isEditing && data.itemsGroups.length > 0 && (*/}
        {/*  <Chip label={`${data.itemsGroups.length} вида товаров`} size="small" />*/}
        {/*)}*/}
        <Box sx={{flex: 1}} />
        {!isEditing && (
          <Button size="small" startIcon={<EditIcon />} onClick={enterEditMode}>
            Изменить
          </Button>
        )}
      </Stack>

      {isEditing ? (
        <Stack spacing={1.5}>
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
              {editRows.map((row, i) => (
                <EditItemRow
                  key={row.key}
                  row={row}
                  onChange={(updated) => updateRow(row.key, updated)}
                  onDelete={() => removeRow(row.key)}
                  disabled={updateMutation.isPending}
                  errorMessage={
                    form.formState.errors?.[i]?.catalogItemWithCharacteristicId?.message
                  }
                />
              ))}
            </TableBody>
          </Table>

          {editRows.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              Нет товаров
            </Typography>
          )}

          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={addRow}
            disabled={updateMutation.isPending}
            sx={{alignSelf: "flex-start"}}
          >
            Добавить товар
          </Button>

          {form.formState.errors.root && (
            <Alert severity="error">{form.formState.errors.root.message}</Alert>
          )}

          <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
            <Button size="small" onClick={cancelEdit} disabled={updateMutation.isPending}>
              Отмена
            </Button>
            <Button
              size="small"
              variant="contained"
              onClick={save}
              disabled={updateMutation.isPending}
              loading={updateMutation.isPending}
            >
              Сохранить
            </Button>
          </Stack>
        </Stack>
      ) : data.itemsGroups.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          Ячейка пуста
        </Typography>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Товар</TableCell>
              <TableCell>Характеристика</TableCell>
              <TableCell align="right">Кол-во</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.itemsGroups.map((group) => (
              <TableRow key={group.id}>
                <TableCell>
                  <Typography variant="body2">
                    {group.catalogItemWithCharacteristic.catalogItem.name}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {group.catalogItemWithCharacteristic.catalogItem.article}
                  </Typography>
                </TableCell>
                <TableCell>{group.catalogItemWithCharacteristic.characteristic || "—"}</TableCell>
                <TableCell align="right">
                  <Chip label={group.count} size="small" color="primary" variant="outlined" />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </Stack>
  );
}

export default NodeDetails;
