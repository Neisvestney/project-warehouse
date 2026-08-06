import {useRef, type ComponentType} from "react";
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

export interface SingleFileControlProps {
  value: DataFileDto | null;
  onChange: (value: DataFileDto | null) => void;
  View: ComponentType<FileViewProps>;
  Input: ComponentType<FileInputProps>;
  accept?: string;
  disabled?: boolean;
  inputLabel?: string;
}

/**
 * Controlled component over a DataFileDto, not over a File: picking a file uploads it immediately
 * and hands the form a ready DTO, so the form only ever stores an identifier.
 */
export default function SingleFileControl({
  value,
  onChange,
  View,
  Input,
  accept,
  disabled,
  inputLabel,
}: SingleFileControlProps) {
  const {showModal} = useModal();
  const {upload, isUploading, error} = useFileUpload();
  const replaceInputRef = useRef<HTMLInputElement | null>(null);

  const pick = async (files: File[]) => {
    if (files.length === 0) return;
    const uploaded = await upload(files[0]).catch(() => null);
    if (uploaded) onChange(uploaded);
  };

  return (
    <Box>
      {value ? (
        <>
          <View
            file={value}
            loading={isUploading}
            disabled={disabled}
            onDelete={() => onChange(null)}
            onReplace={() => replaceInputRef.current?.click()}
            onOpen={() => showModal(FileViewerModal, {files: [viewable(value)]})}
          />
          <input
            ref={replaceInputRef}
            type="file"
            hidden
            accept={accept}
            onChange={(e) => {
              const files = Array.from(e.target.files ?? []);
              // reset so picking the same file twice in a row still fires change
              e.target.value = "";
              void pick(files);
            }}
          />
        </>
      ) : (
        <Input
          onChange={pick}
          loading={isUploading}
          disabled={disabled}
          accept={accept}
          label={inputLabel}
        />
      )}

      {error && <FormHelperText error>{extractErrorMessage(error)}</FormHelperText>}
    </Box>
  );
}
