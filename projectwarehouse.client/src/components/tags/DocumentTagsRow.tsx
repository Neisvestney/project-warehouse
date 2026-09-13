import {useState} from "react";
import {Box, Button, IconButton, Stack} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import InfoRow from "@/components/InfoRow";
import DocumentTagsAutocomplete from "@/components/tags/DocumentTagsAutocomplete";
import TagChips from "@/components/tags/TagChips";
import type {DocumentTag, DocumentTagKind} from "@/components/tags/documentTags";

type DocumentTagsRowProps = {
  kind: DocumentTagKind;
  value: DocumentTag[];
  canEdit: boolean;
  /** Persists the full tag set; the owner writes the returned document back into its cache. */
  save: (tagIds: string[]) => Promise<unknown>;
};

/**
 * «Теги» info row of a document page. Tags are editable in any document status, so the row edits
 * itself instead of joining the status-gated info form, and every change is saved right away.
 */
function DocumentTagsRow({kind, value, canEdit, save}: DocumentTagsRowProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const handleChange = async (tags: DocumentTag[]) => {
    setIsSaving(true);
    try {
      await save(tags.map((t) => t.id));
    } catch {
      // The mutation's global error handler already reports it; the chips stay on the saved set
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <InfoRow
      label="Теги"
      value={
        isEditing ? (
          // InfoRow's value cell is sized by its content, so the picker needs a width of its own
          <Stack
            direction="row"
            spacing={1}
            sx={{alignItems: "center", width: {xs: "100%", sm: 480}, maxWidth: "100%"}}
          >
            <Box sx={{flex: 1, minWidth: 0}}>
              <DocumentTagsAutocomplete
                kind={kind}
                value={value}
                onChange={handleChange}
                disabled={isSaving}
              />
            </Box>
            <Button size="small" onClick={() => setIsEditing(false)} disabled={isSaving}>
              Готово
            </Button>
          </Stack>
        ) : (
          <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
            <TagChips tags={value} />
            {canEdit && (
              <IconButton size="small" title="Изменить теги" onClick={() => setIsEditing(true)}>
                <EditIcon fontSize="small" />
              </IconButton>
            )}
          </Stack>
        )
      }
    />
  );
}

export default DocumentTagsRow;
