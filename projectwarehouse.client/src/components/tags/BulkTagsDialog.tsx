import {useState, type ReactNode} from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import type {TagBatchOperation} from "@/api/types.gen";
import {useBackClosable} from "@/hooks/useBackClosable";
import DocumentTagsAutocomplete from "./DocumentTagsAutocomplete";
import type {DocumentTag, DocumentTagKind} from "./documentTags";

interface BulkTagsDialogProps {
  kind: DocumentTagKind;
  open: boolean;
  isPending: boolean;
  /** Line above the form, e.g. how many documents the change touches. */
  summary?: ReactNode;
  error?: string | null;
  onClose: () => void;
  onConfirm: (tagId: string, operation: TagBatchOperation) => void;
}

/** Adds or removes one tag on a selection of documents of one kind. */
function BulkTagsDialog({
  kind,
  open,
  isPending,
  summary,
  error,
  onClose,
  onConfirm,
}: BulkTagsDialogProps) {
  const [operation, setOperation] = useState<TagBatchOperation>("add");
  const [tag, setTag] = useState<DocumentTag | null>(null);

  useBackClosable(open && !isPending, onClose);

  function reset() {
    setOperation("add");
    setTag(null);
  }

  return (
    <Dialog
      open={open}
      onClose={isPending ? undefined : onClose}
      fullWidth
      maxWidth="xs"
      slotProps={{transition: {onExited: reset}}}
    >
      <DialogTitle>Теги</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          {summary && <Typography variant="body2">{summary}</Typography>}
          <ToggleButtonGroup
            exclusive
            fullWidth
            size="small"
            color="primary"
            value={operation}
            onChange={(_, v: TagBatchOperation | null) => v && setOperation(v)}
            disabled={isPending}
          >
            <ToggleButton value="add">Добавить</ToggleButton>
            <ToggleButton value="remove">Удалить</ToggleButton>
          </ToggleButtonGroup>
          <DocumentTagsAutocomplete
            kind={kind}
            value={tag ? [tag] : []}
            // the picker is a multiselect; the latest pick replaces the previous one
            onChange={(tags) => setTag(tags.at(-1) ?? null)}
            disabled={isPending}
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button
          variant="contained"
          color={operation === "remove" ? "error" : "primary"}
          loading={isPending}
          disabled={!tag}
          onClick={() => tag && onConfirm(tag.id, operation)}
        >
          {operation === "add" ? "Добавить" : "Удалить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default BulkTagsDialog;
