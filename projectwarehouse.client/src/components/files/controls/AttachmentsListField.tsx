import type {AttachmentLinkValue} from "../attachmentLink";
import AddFileDropTile from "../inputs/AddFileDropTile";
import AttachmentTileFileView from "../views/AttachmentTileFileView";
import FileListControl from "./FileListControl";

export interface AttachmentsListFieldProps {
  value: AttachmentLinkValue[];
  onChange: (value: AttachmentLinkValue[]) => void;
  disabled?: boolean;
  inputLabel?: string;
}

/**
 * File attachment list shared by every 1:N attachment point (receipts, write-offs, orders,
 * stocktakes, …). Any file type is accepted — these are scanned documents and photos, not just
 * images — so tiles fall back to a type icon, but the layout is still a wrapping grid.
 */
export default function AttachmentsListField({
  value,
  onChange,
  disabled,
  inputLabel = "Добавить файл",
}: AttachmentsListFieldProps) {
  return (
    <FileListControl
      value={value.map((link) => link.file)}
      // keep entityId for files that were already saved, so the join row is updated, not recreated
      onChange={(files) =>
        onChange(files.map((file) => value.find((link) => link.file.id === file.id) ?? {file}))
      }
      View={AttachmentTileFileView}
      Input={AddFileDropTile}
      direction="row"
      disabled={disabled}
      sortable
      inlineInput
      inputLabel={inputLabel}
    />
  );
}
