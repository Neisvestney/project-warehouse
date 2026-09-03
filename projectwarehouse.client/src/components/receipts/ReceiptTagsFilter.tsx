import {useCallback, useMemo} from "react";
import {Autocomplete, Chip, Paper, type PaperProps, TextField} from "@mui/material";
import type {SxProps, Theme} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {receiptsGetTagsOptions} from "@/api/@tanstack/react-query.gen";
import SelectAllHeader from "@/components/SelectAllHeader";

type ReceiptTagsFilterProps = {
  /** Selected tag IDs. */
  value: string[];
  onChange: (value: string[]) => void;
  label?: string;
  sx?: SxProps<Theme>;
};

/** Multiselect over the full receipt tag list — small enough to load at once and filter client-side. */
function ReceiptTagsFilter({value, onChange, label = "Теги", sx}: ReceiptTagsFilterProps) {
  const tagsQuery = useQuery(receiptsGetTagsOptions({}));
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
      renderInput={(params) => <TextField {...params} label={label} />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((tag, index) => (
          <Chip label={tag.name} size="small" {...getItemProps({index})} key={tag.id} />
        ))
      }
    />
  );
}

export default ReceiptTagsFilter;
