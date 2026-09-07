import {useRef, useState, type DragEvent} from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import AddIcon from "@mui/icons-material/Add";
import type {FileInputProps} from "./AddFileInput";

const SIZE = 120;

/**
 * Grid tile that doubles as a drop zone: click opens the file picker, dragging files over it
 * uploads them the same way. Matches `AttachmentTileFileView`'s size so it sits in the grid
 * as just another tile rather than a control below it.
 */
export default function AddFileDropTile({
  onChange,
  loading,
  disabled,
  accept,
  multiple,
  label = "Добавить файл",
}: FileInputProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const interactive = !disabled && !loading;

  const pick = (files: File[]) => {
    if (files.length > 0) onChange(files);
  };

  return (
    <Box
      onClick={() => interactive && inputRef.current?.click()}
      onDragOver={(e: DragEvent) => {
        if (!interactive) return;
        e.preventDefault();
        setDragOver(true);
      }}
      onDragLeave={() => setDragOver(false)}
      onDrop={(e: DragEvent) => {
        e.preventDefault();
        setDragOver(false);
        if (!interactive) return;
        pick(Array.from(e.dataTransfer.files));
      }}
      sx={{
        width: SIZE,
        height: SIZE,
        borderRadius: 1,
        border: "2px dashed",
        borderColor: dragOver ? "primary.main" : "divider",
        bgcolor: dragOver ? "action.hover" : "transparent",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 0.5,
        color: "text.secondary",
        cursor: interactive ? "pointer" : "default",
        transition: "border-color 120ms, background-color 120ms",
        "&:hover": interactive ? {borderColor: "primary.main"} : undefined,
      }}
    >
      {loading ? (
        <CircularProgress size={22} />
      ) : (
        <>
          <AddIcon />
          <Typography variant="caption" sx={{textAlign: "center", px: 1}}>
            {label}
          </Typography>
        </>
      )}
      <input
        ref={inputRef}
        type="file"
        hidden
        accept={accept}
        multiple={multiple}
        disabled={disabled}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          // reset so picking the same file twice in a row still fires change
          e.target.value = "";
          pick(files);
        }}
      />
    </Box>
  );
}
