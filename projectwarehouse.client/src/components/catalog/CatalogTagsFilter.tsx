import {useCallback, useMemo} from "react";
import {Autocomplete, Chip, Paper, type PaperProps, TextField} from "@mui/material";
import type {SxProps, Theme} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {catalogGetTagsOptions} from "@/api/@tanstack/react-query.gen";
import SelectAllHeader from "@/components/SelectAllHeader";

type CatalogTagsFilterProps = {
  /** Selected tag IDs. */
  value: string[];
  onChange: (value: string[]) => void;
  label?: string;
  autoFocus?: boolean;
  sx?: SxProps<Theme>;
};

/** Multiselect over the full tag list — small enough to load at once and filter client-side. */
function CatalogTagsFilter({
  value,
  onChange,
  label = "Теги",
  autoFocus,
  sx,
}: CatalogTagsFilterProps) {
  const tagsQuery = useQuery(catalogGetTagsOptions({}));
  const allTags = useMemo(() => tagsQuery.data ?? [], [tagsQuery.data]);

  const selectedTags = useMemo(
    () => value.map((id) => allTags.find((t) => t.id === id)).filter((t) => t !== undefined),
    [value, allTags],
  );

  // Slot component must keep a stable identity, otherwise the popup remounts on every render
  const TagsPaper = useCallback(
    ({children, ...paperProps}: PaperProps) => (
      <Paper {...paperProps}>
        <SelectAllHeader
          selectAllDisabled={allTags.length === 0 || value.length === allTags.length}
          clearDisabled={value.length === 0}
          onSelectAll={() => onChange(allTags.map((t) => t.id))}
          onClear={() => onChange([])}
        />
        {children}
      </Paper>
    ),
    [allTags, value.length, onChange],
  );

  return (
    <Autocomplete
      multiple
      size="small"
      sx={sx}
      options={allTags}
      value={selectedTags}
      loading={tagsQuery.isLoading}
      slots={{paper: TagsPaper}}
      onChange={(_, v) => onChange(v.map((t) => t.id))}
      getOptionLabel={(t) => t.name}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      noOptionsText="Тегов нет"
      renderInput={(params) => <TextField {...params} label={label} autoFocus={autoFocus} />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((tag, index) => (
          <Chip label={tag.name} size="small" {...getItemProps({index})} key={tag.id} />
        ))
      }
    />
  );
}

export default CatalogTagsFilter;
