import type {SxProps, Theme} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import TagFilterAutocomplete from "@/components/tags/TagFilterAutocomplete";
import {documentTagsQueryOptions, type DocumentTagKind} from "@/components/tags/documentTags";
import type {TagFilter} from "@/features/tags";

type DocumentTagsFilterProps = {
  kind: DocumentTagKind;
  value: TagFilter;
  onChange: (value: TagFilter) => void;
  label?: string;
  sx?: SxProps<Theme>;
};

/** Tag condition over the full tag list of one document type — small enough to load at once and filter client-side. */
function DocumentTagsFilter({kind, ...props}: DocumentTagsFilterProps) {
  const tagsQuery = useQuery(documentTagsQueryOptions(kind));

  return (
    <TagFilterAutocomplete tags={tagsQuery.data ?? []} loading={tagsQuery.isLoading} {...props} />
  );
}

export default DocumentTagsFilter;
