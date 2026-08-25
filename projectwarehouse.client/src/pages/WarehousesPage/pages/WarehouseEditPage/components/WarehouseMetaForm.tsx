import {useState} from "react";
import {Button, IconButton, Stack, Typography} from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import type {Control} from "react-hook-form";
import {useController} from "react-hook-form";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetByIdForPrintOptions} from "@/api/@tanstack/react-query.gen";
import {FormTextField} from "@/components/form/FormTextField";
import {FormTimeZoneField} from "@/components/form/FormTimeZoneField";
import SelectNodeModal from "@/components/receipts/SelectNodeModal";
import type {WarehouseMetaFormValues} from "../warehouseEditStore";
import {formatStoragePlaceNodeName} from "@/components/shared/nodePathUtils";

interface WarehouseMetaFormProps {
  control: Control<WarehouseMetaFormValues>;
  disabled?: boolean;
  warehouseId?: string;
}

function DefaultNodePicker({
  control,
  disabled,
  warehouseId,
}: {
  control: Control<WarehouseMetaFormValues>;
  disabled?: boolean;
  warehouseId: string;
}) {
  const [open, setOpen] = useState(false);

  const {field} = useController({control, name: "defaultStoragePlaceNodeId"});
  const nodeId = field.value;

  const printQuery = useQuery({
    ...warehousesGetByIdForPrintOptions({path: {id: warehouseId}}),
    enabled: !!nodeId,
    meta: {suppressGlobalError: true},
  });

  const nodePath =
    nodeId && printQuery.data
      ? formatStoragePlaceNodeName(printQuery.data.find((n) => n.id === nodeId)?.name ?? [nodeId])
      : null;

  return (
    <>
      <Stack spacing={0.5} sx={{minWidth: 280}}>
        <Typography variant="caption" color="text.secondary">
          Ячейка по умолчанию
        </Typography>
        <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
          <Button variant="outlined" size="small" disabled={disabled} onClick={() => setOpen(true)}>
            Выбрать
          </Button>
          <Typography
            variant="body2"
            sx={{
              color: nodePath ? "text.primary" : "text.disabled",
              fontStyle: nodePath ? "normal" : "italic",
            }}
          >
            {nodePath ?? "Не выбрана"}
          </Typography>
          {nodeId && (
            <IconButton
              size="small"
              disabled={disabled}
              onClick={() => field.onChange(null)}
              title="Сбросить"
            >
              <ClearIcon fontSize="small" />
            </IconButton>
          )}
        </Stack>
      </Stack>

      <SelectNodeModal
        open={open}
        onClose={() => setOpen(false)}
        warehouseId={warehouseId}
        onSelect={(node) => {
          field.onChange(node.nodeId);
          setOpen(false);
        }}
      />
    </>
  );
}

export default function WarehouseMetaForm({
  control,
  disabled,
  warehouseId,
}: WarehouseMetaFormProps) {
  return (
    <Stack direction="column" spacing={2}>
      <Stack direction="row" spacing={2} useFlexGap sx={{flexWrap: "wrap"}}>
        <FormTextField
          control={control}
          name="name"
          label="Название"
          rules={{required: "Обязательное поле"}}
          disabled={disabled}
          sx={{minWidth: 240}}
        />
        <FormTextField
          control={control}
          name="width"
          label="Ширина (м)"
          type="number"
          rules={{required: "Обязательное поле", min: {value: 1, message: "Минимум 1"}}}
          disabled={disabled}
          sx={{width: 160}}
        />
        <FormTextField
          control={control}
          name="height"
          label="Длина (м)"
          type="number"
          rules={{required: "Обязательное поле", min: {value: 1, message: "Минимум 1"}}}
          disabled={disabled}
          sx={{width: 160}}
        />
      </Stack>
      {warehouseId && (
        <>
          <FormTimeZoneField
            control={control}
            name="timeZoneId"
            helperText="Пусто — пояс вызывающего или сервера"
            disabled={disabled}
            sx={{maxWidth: 320}}
          />
          <DefaultNodePicker control={control} disabled={disabled} warehouseId={warehouseId} />
        </>
      )}
    </Stack>
  );
}
