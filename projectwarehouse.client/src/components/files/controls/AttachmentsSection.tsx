import {useState} from "react";
import {Alert, Box, Button, Paper, Stack, Typography} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import type {DataFileLinkDto, DataFileLinkRequest} from "@/api";
import {extractErrorMessage} from "@/utils/errorUtils";
import {
  toAttachmentLinks,
  mapAttachmentsToRequest,
  type AttachmentLinkValue,
} from "../attachmentLink";
import AttachmentsListField from "./AttachmentsListField";

export interface AttachmentsSectionProps {
  value: DataFileLinkDto[];
  /** Whether the current user may edit attachments — independent of the document's status. */
  canEdit: boolean;
  save: (attachments: DataFileLinkRequest[]) => Promise<unknown>;
}

/**
 * Standalone attachments block for a document (receipt, write-off, order, stocktake, …). Deliberately
 * separate from the document's own edit form and its status gate: attaching a scan or a photo is not
 * a change to the document's business fields, so it stays editable in any status.
 */
function AttachmentsSection({value, canEdit, save}: AttachmentsSectionProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState<AttachmentLinkValue[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const startEditing = () => {
    setDraft(toAttachmentLinks(value));
    setError(null);
    setIsEditing(true);
  };

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    try {
      await save(mapAttachmentsToRequest(draft));
      setIsEditing(false);
    } catch (e) {
      setError(extractErrorMessage(e));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Paper>
      <Stack spacing={1.5} sx={{p: 3}}>
        <Typography variant="subtitle1">Вложения</Typography>

        {isEditing ? (
          <>
            <AttachmentsListField value={draft} onChange={setDraft} disabled={isSaving} />
            {error && <Alert severity="error">{error}</Alert>}
            <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end"}}>
              <Button size="small" onClick={() => setIsEditing(false)} disabled={isSaving}>
                Отмена
              </Button>
              <Button
                size="small"
                variant="contained"
                startIcon={<SaveIcon />}
                onClick={handleSave}
                disabled={isSaving}
                loading={isSaving}
              >
                Сохранить
              </Button>
            </Stack>
          </>
        ) : (
          <>
            {value.length > 0 ? (
              <AttachmentsListField value={toAttachmentLinks(value)} onChange={() => {}} disabled />
            ) : (
              <Typography variant="body2" color="text.secondary">
                Нет вложений
              </Typography>
            )}
            {canEdit && (
              <Box>
                <Button
                  size="small"
                  startIcon={<EditIcon fontSize="small" />}
                  onClick={startEditing}
                >
                  Редактировать
                </Button>
              </Box>
            )}
          </>
        )}
      </Stack>
    </Paper>
  );
}

export default AttachmentsSection;
