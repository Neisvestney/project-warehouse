import {Stack} from "@mui/material";
import type {Control} from "react-hook-form";
import {FormTextField} from "@/components/form/FormTextField";
import type {WarehouseMetaFormValues} from "../warehouseEditStore";

interface WarehouseMetaFormProps {
  control: Control<WarehouseMetaFormValues>;
  disabled?: boolean;
}

export default function WarehouseMetaForm({control, disabled}: WarehouseMetaFormProps) {
  return (
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
        label="Высота (м)"
        type="number"
        rules={{required: "Обязательное поле", min: {value: 1, message: "Минимум 1"}}}
        disabled={disabled}
        sx={{width: 160}}
      />
    </Stack>
  );
}
