import {useRef} from "react";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import AddPhotoAlternateOutlinedIcon from "@mui/icons-material/AddPhotoAlternateOutlined";

export interface FileInputProps {
  onChange: (files: File[]) => void;
  loading?: boolean;
  disabled?: boolean;
  accept?: string;
  multiple?: boolean;
  label?: string;
}

/** Plain "add a file" button. Knows nothing about the API — the control layer does the uploading. */
export default function AddFileInput({
  onChange,
  loading,
  disabled,
  accept,
  multiple,
  label = "Добавить файл",
}: FileInputProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <>
      <Button
        variant="outlined"
        size="small"
        disabled={disabled || loading}
        startIcon={
          loading ? (
            <CircularProgress size={16} />
          ) : (
            <AddPhotoAlternateOutlinedIcon fontSize="small" />
          )
        }
        onClick={() => inputRef.current?.click()}
      >
        {label}
      </Button>
      <input
        ref={inputRef}
        type="file"
        hidden
        accept={accept}
        multiple={multiple}
        onChange={(e) => {
          const files = Array.from(e.target.files ?? []);
          // reset so picking the same file twice in a row still fires change
          e.target.value = "";
          if (files.length > 0) onChange(files);
        }}
      />
    </>
  );
}
