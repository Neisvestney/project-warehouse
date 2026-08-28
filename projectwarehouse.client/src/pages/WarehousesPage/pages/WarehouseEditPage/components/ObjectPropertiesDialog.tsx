import {useEffect} from "react";
import {observer} from "mobx-react-lite";
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {Controller, useForm} from "react-hook-form";
import type {WarehouseLayoutObjectType} from "@/api/types.gen";
import {useWarehouseEditStore} from "../WarehouseEditStoreContext";
import {useRetainedValue} from "@/hooks/useRetainedValue";

interface PropertiesFormValues {
  name: string;
  x: string;
  y: string;
  width: string;
  height: string;
  rotation: string;
  type: WarehouseLayoutObjectType;
}

interface ObjectPropertiesDialogProps {
  open: boolean;
  tempId: string | null;
  onClose: () => void;
}

const LAYOUT_OBJECT_TYPES: {value: WarehouseLayoutObjectType; label: string}[] = [
  {value: "wall", label: "Стена"},
  {value: "passage", label: "Проход"},
];

export default observer(function ObjectPropertiesDialog({
  open,
  tempId,
  onClose,
}: ObjectPropertiesDialogProps) {
  "use no memo";

  const store = useWarehouseEditStore();

  const [shownTempId, releaseShownTempId] = useRetainedValue(tempId);

  const sp = shownTempId ? store.storagePlaces.find((s) => s.tempId === shownTempId) : undefined;
  const lo = shownTempId ? store.layoutObjects.find((l) => l.tempId === shownTempId) : undefined;
  const obj = sp
    ? {kind: "storagePlace" as const, data: sp}
    : lo
      ? {kind: "layoutObject" as const, data: lo}
      : null;

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: {errors},
  } = useForm<PropertiesFormValues>();

  useEffect(() => {
    if (!open || !obj) return;
    reset({
      name: obj.kind === "storagePlace" ? obj.data.name : "",
      x: obj.data.x.toFixed(2),
      y: obj.data.y.toFixed(2),
      width: obj.data.width.toFixed(2),
      height: obj.data.height.toFixed(2),
      rotation: obj.data.rotation.toFixed(1),
      type: obj.kind === "layoutObject" ? obj.data.type : "wall",
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, tempId]);

  const onSubmit = (values: PropertiesFormValues) => {
    if (!tempId || !obj) return;
    const geometry = {
      x: parseFloat(values.x),
      y: parseFloat(values.y),
      width: parseFloat(values.width),
      height: parseFloat(values.height),
      rotation: parseFloat(values.rotation),
    };
    if (obj.kind === "storagePlace") {
      store.updateStoragePlace(tempId, {...geometry, name: values.name});
    } else {
      store.updateLayoutObject(tempId, {...geometry, type: values.type});
    }
    onClose();
  };

  useBackClosable(open, onClose);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      fullWidth
      slotProps={{
        transition: {onExited: releaseShownTempId},
        paper: {sx: {pointerEvents: open ? undefined : "none"}},
      }}
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <DialogTitle>
          {obj?.kind === "storagePlace" ? "Место хранения" : "Объект планировки"}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{pt: 1}}>
            {obj?.kind === "storagePlace" && (
              <TextField
                label="Название"
                size="small"
                fullWidth
                error={!!errors.name}
                helperText={errors.name?.message}
                {...register("name", {required: "Обязательное поле"})}
              />
            )}
            {obj?.kind === "layoutObject" && (
              <Controller
                name="type"
                control={control}
                render={({field}) => (
                  <TextField {...field} select label="Тип" size="small" fullWidth>
                    {LAYOUT_OBJECT_TYPES.map((t) => (
                      <MenuItem key={t.value} value={t.value}>
                        {t.label}
                      </MenuItem>
                    ))}
                  </TextField>
                )}
              />
            )}
            <Stack direction="row" spacing={1}>
              <TextField
                label="X (м)"
                size="small"
                fullWidth
                type="number"
                error={!!errors.x}
                slotProps={{htmlInput: {step: 0.01}}}
                {...register("x", {required: true, validate: (v) => !isNaN(parseFloat(v))})}
              />
              <TextField
                label="Y (м)"
                size="small"
                fullWidth
                type="number"
                error={!!errors.y}
                slotProps={{htmlInput: {step: 0.01}}}
                {...register("y", {required: true, validate: (v) => !isNaN(parseFloat(v))})}
              />
            </Stack>
            <Stack direction="row" spacing={1}>
              <TextField
                label="Ширина (м)"
                size="small"
                fullWidth
                type="number"
                error={!!errors.width}
                slotProps={{htmlInput: {step: 0.01, min: 0.01}}}
                {...register("width", {
                  required: true,
                  min: 0.01,
                  validate: (v) => !isNaN(parseFloat(v)),
                })}
              />
              <TextField
                label="Длина (м)"
                size="small"
                fullWidth
                type="number"
                error={!!errors.height}
                slotProps={{htmlInput: {step: 0.01, min: 0.01}}}
                {...register("height", {
                  required: true,
                  min: 0.01,
                  validate: (v) => !isNaN(parseFloat(v)),
                })}
              />
            </Stack>
            <TextField
              label="Поворот (°)"
              size="small"
              fullWidth
              type="number"
              error={!!errors.rotation}
              slotProps={{htmlInput: {step: 1}}}
              {...register("rotation", {required: true, validate: (v) => !isNaN(parseFloat(v))})}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} color="inherit">
            Отмена
          </Button>
          <Button type="submit" variant="contained">
            Применить
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
});
