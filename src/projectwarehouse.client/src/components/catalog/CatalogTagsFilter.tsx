import type {SxProps, Theme} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {catalogGetTagsOptions} from "@/api/@tanstack/react-query.gen";
import TagFilterAutocomplete from "@/components/tags/TagFilterAutocomplete";
import type {TagFilter} from "@/features/tags";

type CatalogTagsFilterProps = {
  value: TagFilter;
  onChange: (value: TagFilter) => void;
  label?: string;
  autoFocus?: boolean;
  sx?: SxProps<Theme>;
};

/** Tag condition over the full catalog tag list — small enough to load at once and filter client-side. */
function CatalogTagsFilter(props: CatalogTagsFilterProps) {
  const tagsQuery = useQuery(catalogGetTagsOptions({}));

  return (
    <TagFilterAutocomplete tags={tagsQuery.data ?? []} loading={tagsQuery.isLoading} {...props} />
  );
}

export default CatalogTagsFilter;
