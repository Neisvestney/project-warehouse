import {useState, type ComponentType, type ReactNode} from "react";
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
  arrayMove,
  rectSortingStrategy,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import {CSS} from "@dnd-kit/utilities";
import Box from "@mui/material/Box";
import FormHelperText from "@mui/material/FormHelperText";
import type {DataFileDto} from "@/api";
import {useModal} from "@/hooks/useModal";
import {extractErrorMessage} from "@/utils/errorUtils";
import {useFileUpload} from "../hooks/useFileUpload";
import type {FileInputProps} from "../inputs/AddFileInput";
import type {FileViewProps} from "../views/fileViewProps";
import FileViewerModal from "../viewer/FileViewerModal";
import {viewable} from "../viewer/viewableFile";

export interface FileListControlProps {
  value: DataFileDto[];
  onChange: (value: DataFileDto[]) => void;
  View: ComponentType<FileViewProps>;
  Input: ComponentType<FileInputProps>;
  accept?: string;
  disabled?: boolean;
  inputLabel?: string;
  /** Layout of the rendered views. Rows stack, thumbnails wrap. */
  direction?: "row" | "column";
  /** Drag to reorder. The owner decides what the position means — usually the `order` column. */
  sortable?: boolean;
}

export default function FileListControl({
  value,
  onChange,
  View,
  Input,
  accept,
  disabled,
  inputLabel,
  direction = "row",
  sortable,
}: FileListControlProps) {
  const {showModal} = useModal();
  const {upload, isUploading} = useFileUpload();
  // distance keeps a tap from becoming a drag, so click-to-open still works on the same tile
  const sensors = useSensors(
    useSensor(PointerSensor, {activationConstraint: {distance: 5}}),
    useSensor(KeyboardSensor, {coordinateGetter: sortableKeyboardCoordinates}),
  );
  // the hook's own error only holds the last failure — a batch needs one message per file
  const [failures, setFailures] = useState<{name: string; message: string}[]>([]);

  const pick = async (files: File[]) => {
    setFailures([]);
    // sequential rather than Promise.all: a burst of parallel uploads is what fills the disk on a slip
    const uploaded: DataFileDto[] = [];
    const failed: {name: string; message: string}[] = [];
    for (const file of files) {
      try {
        uploaded.push(await upload(file));
      } catch (e) {
        failed.push({name: file.name, message: extractErrorMessage(e)});
      }
    }
    setFailures(failed);
    if (uploaded.length > 0) onChange([...value, ...uploaded]);
  };

  const openAt = (index: number) =>
    showModal(FileViewerModal, {files: value.map(viewable), initialIndex: index});

  const canSort = !!sortable && !disabled && value.length > 1;

  const handleDragEnd = ({active, over}: DragEndEvent) => {
    if (!over || active.id === over.id) return;
    const from = value.findIndex((f) => f.id === active.id);
    const to = value.findIndex((f) => f.id === over.id);
    if (from < 0 || to < 0) return;
    onChange(arrayMove(value, from, to));
  };

  const viewProps = (file: DataFileDto, index: number) => ({
    file,
    disabled,
    onDelete: () => onChange(value.filter((f) => f.id !== file.id)),
    onOpen: () => openAt(index),
  });

  // the wrapper only exists while sorting, so the plain layout stays exactly as it was
  const tiles = value.map((file, index) =>
    canSort ? (
      <SortableTile key={file.id} id={file.id} direction={direction}>
        <View {...viewProps(file, index)} />
      </SortableTile>
    ) : (
      <View key={file.id} {...viewProps(file, index)} />
    ),
  );

  return (
    <Box sx={{display: "flex", flexDirection: "column", gap: 1}}>
      {value.length > 0 && (
        <Box
          sx={{
            display: "flex",
            flexDirection: direction,
            flexWrap: direction === "row" ? "wrap" : "nowrap",
            gap: 1,
          }}
        >
          {canSort ? (
            <DndContext
              sensors={sensors}
              collisionDetection={closestCenter}
              onDragEnd={handleDragEnd}
            >
              <SortableContext
                items={value.map((f) => f.id)}
                strategy={direction === "row" ? rectSortingStrategy : verticalListSortingStrategy}
              >
                {tiles}
              </SortableContext>
            </DndContext>
          ) : (
            tiles
          )}
        </Box>
      )}

      {!disabled && (
        <Input
          onChange={pick}
          loading={isUploading}
          disabled={disabled}
          accept={accept}
          multiple
          label={inputLabel}
        />
      )}

      {failures.map((f, i) => (
        <FormHelperText key={`${f.name}-${i}`} error>
          {f.name} — {f.message}
        </FormHelperText>
      ))}
    </Box>
  );
}

/**
 * The whole tile is the handle — a photo grid is dragged by the photo, not by a grip icon. The views
 * stay pure: dnd-kit's listeners live here, never in `FileViewProps`.
 */
function SortableTile({
  id,
  direction,
  children,
}: {
  id: string;
  direction: "row" | "column";
  children: ReactNode;
}) {
  const {attributes, listeners, setNodeRef, transform, transition, isDragging} = useSortable({id});

  return (
    <Box
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      sx={{
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.4 : 1,
        // without this a touch drag scrolls the drawer instead of moving the tile
        touchAction: "none",
        cursor: "grab",
        ...(direction === "row" ? {flex: "0 0 auto"} : {width: "100%"}),
      }}
    >
      {children}
    </Box>
  );
}
