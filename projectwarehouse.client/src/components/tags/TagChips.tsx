import {Chip, Stack} from "@mui/material";
import type {DocumentTag} from "@/components/tags/documentTags";

/** Read-only tag chips for table cells and info rows; a dash when there are none. */
function TagChips({tags}: {tags: DocumentTag[]}) {
  if (tags.length === 0) return "—";

  return (
    <Stack direction="row" spacing={0.5} sx={{flexWrap: "wrap", gap: 0.5}}>
      {tags.map((tag) => (
        <Chip key={tag.id} label={tag.name} size="small" />
      ))}
    </Stack>
  );
}

export default TagChips;
