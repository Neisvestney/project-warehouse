import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import {Box, Button, Divider, IconButton, Stack, Typography} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import type {Control, UseFormSetValue} from "react-hook-form";
import {Controller, useFieldArray, useWatch} from "react-hook-form";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import {ClampedIntegerField} from "@/components/form/ClampedIntegerField";
import type {CatalogItemFormValues} from "./catalogItemFormValues";

export default function BundleComponentsEditor({
  control,
  setValue,
  isPending,
}: {
  control: Control<CatalogItemFormValues>;
  setValue: UseFormSetValue<CatalogItemFormValues>;
  isPending: boolean;
}) {
  const {fields, append, remove, move} = useFieldArray({control, name: "components"});
  // distance keeps a click on the row's inputs from turning into a drag
  const sensors = useSensors(
    useSensor(PointerSensor, {activationConstraint: {distance: 5}}),
    useSensor(KeyboardSensor, {coordinateGetter: sortableKeyboardCoordinates}),
  );

  const canSort = !isPending && fields.length > 1;

  const handleDragEnd = ({active, over}: DragEndEvent) => {
    if (!over || active.id === over.id) return;
    const from = fields.findIndex((f) => f.id === active.id);
    const to = fields.findIndex((f) => f.id === over.id);
    if (from < 0 || to < 0) return;
    move(from, to);
  };

  const rows = fields.map((field, index) => (
    <BundleComponentRow
      key={field.id}
      id={field.id}
      control={control}
      setValue={setValue}
      index={index}
      onRemove={() => remove(index)}
      isPending={isPending}
      canSort={canSort}
    />
  ));

  return (
    <>
      <Divider />
      <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "center"}}>
        <Typography variant="subtitle2">Компоненты</Typography>
        <Button
          size="small"
          startIcon={<AddIcon />}
          onClick={() => append({component: null, quantity: 1})}
          disabled={isPending}
        >
          Добавить
        </Button>
      </Stack>
      {canSort ? (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
          <SortableContext items={fields.map((f) => f.id)} strategy={verticalListSortingStrategy}>
            <Stack spacing={2}>{rows}</Stack>
          </SortableContext>
        </DndContext>
      ) : (
        <Stack spacing={2}>{rows}</Stack>
      )}
    </>
  );
}

function BundleComponentRow({
  id,
  control,
  setValue,
  index,
  onRemove,
  isPending,
  canSort,
}: {
  id: string;
  control: Control<CatalogItemFormValues>;
  setValue: UseFormSetValue<CatalogItemFormValues>;
  index: number;
  onRemove: () => void;
  isPending: boolean;
  canSort: boolean;
}) {
  const component = useWatch({control, name: `components.${index}.component`});
  const {attributes, listeners, setNodeRef, transform, transition, isDragging} = useSortable({
    id,
    disabled: !canSort,
  });

  return (
    <Stack
      ref={setNodeRef}
      direction="row"
      spacing={1}
      sx={{
        alignItems: "flex-start",
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.4 : 1,
      }}
    >
      <Box
        {...attributes}
        {...listeners}
        sx={{
          display: "flex",
          alignItems: "center",
          // matches the height of a small input, so the handle sits on the field's centre line
          height: 40,
          cursor: "grab",
          touchAction: "none",
          color: "text.disabled",
          pointerEvents: !canSort ? "none" : undefined,
        }}
      >
        <DragIndicatorIcon fontSize="small" />
      </Box>
      <Box sx={{flex: 1}}>
        <CatalogItemsSelect
          value={component?.id ?? null}
          onChange={(id) => {
            if (!id) setValue(`components.${index}.component`, null);
          }}
          onDtoChange={(dto) => setValue(`components.${index}.component`, dto)}
          types={["standard", "unit", "productGroup", "variation"]}
          label="Позиция"
          disabled={isPending}
          size="small"
          textFieldProps={{size: "small"}}
        />
      </Box>
      <Controller
        control={control}
        name={`components.${index}.quantity`}
        rules={{required: true, min: {value: 1, message: "Мин. 1"}}}
        render={({field: f, fieldState}) => (
          <ClampedIntegerField
            name={f.name}
            inputRef={f.ref}
            value={f.value}
            onCommit={f.onChange}
            label="Кол-во"
            size="small"
            sx={{width: 90}}
            disabled={isPending}
            error={!!fieldState.error}
            helperText={fieldState.error?.message}
          />
        )}
      />
      <Box sx={{display: "flex", alignItems: "center", height: 40}}>
        <IconButton size="small" onClick={onRemove} disabled={isPending}>
          <DeleteIcon fontSize="small" />
        </IconButton>
      </Box>
    </Stack>
  );
}
