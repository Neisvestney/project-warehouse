import {useRef, useState} from "react";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import type {FileInputProps} from "./AddFileInput";

/**
 * Drop zone on native drag events — there is no dropzone library in the project, and @dnd-kit is
 * for sorting, not for accepting files from the OS.
 */
export default function AreaFileInput({
  onChange,
  loading,
  disabled,
  accept,
  multiple,
  label = "Перетащите файлы сюда или нажмите для выбора",
}: FileInputProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [dragging, setDragging] = useState(false);

  const emit = (files: File[]) => {
    if (files.length > 0) onChange(files);
  };

  return (
    <Box
      onDragOver={(e) => {
        if (disabled || loading) return;
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault();
        setDragging(false);
        if (disabled || loading) return;
        emit(Array.from(e.dataTransfer.files ?? []));
      }}
      onClick={() => !disabled && !loading && inputRef.current?.click()}
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 1,
        p: 3,
        borderRadius: 1,
        border: "1px dashed",
        borderColor: dragging ? "primary.main" : "divider",
        bgcolor: dragging ? "action.hover" : "transparent",
        cursor: disabled || loading ? "default" : "pointer",
        opacity: disabled ? 0.5 : 1,
        transition: "border-color 120ms, background-color 120ms",
      }}
    >
      {loading ? <CircularProgress size={24} /> : <CloudUploadOutlinedIcon color="action" />}
      <Typography variant="body2" color="text.secondary" align="center">
        {label}
      </Typography>
      <input
        ref={inputRef}
        type="file"
        hidden
        accept={accept}
        multiple={multiple}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          e.target.value = "";
          emit(files);
        }}
      />
    </Box>
  );
}
