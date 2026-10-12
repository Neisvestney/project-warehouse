import {createContext, useContext} from "react";
import {
  Autocomplete,
  Box,
  Chip,
  IconButton,
  Paper,
  type PaperProps,
  TextField,
  Tooltip,
} from "@mui/material";
import type {SxProps, Theme} from "@mui/material";
import BlockIcon from "@mui/icons-material/Block";
import CheckIcon from "@mui/icons-material/Check";
import SelectAllHeader from "@/components/SelectAllHeader";
import type {TagFilter} from "@/features/tags";

export type TagFilterOption = {id: string; name: string};

type TagFilterAutocompleteProps = {
  tags: TagFilterOption[];
  loading?: boolean;
  value: TagFilter;
  onChange: (value: TagFilter) => void;
  label?: string;
  autoFocus?: boolean;
  sx?: SxProps<Theme>;
};

const without = (ids: string[], id: string) => ids.filter((x) => x !== id);

type TagsPaperState = {
  selectAllDisabled: boolean;
  clearDisabled: boolean;
  onSelectAll: () => void;
  onClear: () => void;
};

// The paper slot reads its state from context so the slot component keeps one identity: a new identity
// remounts the listbox and drops its scroll position on every change of the value.
const TagsPaperContext = createContext<TagsPaperState | null>(null);

function TagsPaper({children, ...paperProps}: PaperProps) {
  const state = useContext(TagsPaperContext);
  return (
    <Paper {...paperProps}>
      {state && <SelectAllHeader {...state} />}
      {children}
    </Paper>
  );
}

/** Multiselect over a fully loaded tag list where each picked tag is either included or excluded. */
function TagFilterAutocomplete({
  tags,
  loading,
  value,
  onChange,
  label = "Теги",
  autoFocus,
  sx,
}: TagFilterAutocompleteProps) {
  const isExcluded = (id: string) => value.exclude.includes(id);

  // Tag list order, not pick order: switching a chip between the two halves must not move it
  const selectedTags = tags.filter(
    (t) => value.include.includes(t.id) || value.exclude.includes(t.id),
  );

  const toggleExcluded = (id: string) =>
    onChange(
      isExcluded(id)
        ? {include: value.include, exclude: without(value.exclude, id)}
        : {include: without(value.include, id), exclude: [...value.exclude, id]},
    );

  const switchMode = (id: string) =>
    onChange(
      isExcluded(id)
        ? {include: [...value.include, id], exclude: without(value.exclude, id)}
        : {include: without(value.include, id), exclude: [...value.exclude, id]},
    );

  const pickedCount = value.include.length + value.exclude.length;

  const paperState: TagsPaperState = {
    selectAllDisabled: tags.length === 0 || pickedCount === tags.length,
    clearDisabled: pickedCount === 0,
    onSelectAll: () =>
      onChange({
        include: tags.map((t) => t.id).filter((id) => !value.exclude.includes(id)),
        exclude: value.exclude,
      }),
    onClear: () => onChange({include: [], exclude: []}),
  };

  return (
    <TagsPaperContext.Provider value={paperState}>
      <Autocomplete
        multiple
        size="small"
        sx={sx}
        options={tags}
        value={selectedTags}
        loading={loading}
        slots={{paper: TagsPaper}}
        onChange={(_, picked) => {
          const ids = picked.map((t) => t.id);
          onChange({
            include: ids.filter((id) => !value.exclude.includes(id)),
            exclude: value.exclude.filter((id) => ids.includes(id)),
          });
        }}
        getOptionLabel={(t) => t.name}
        isOptionEqualToValue={(a, b) => a.id === b.id}
        noOptionsText="Тегов нет"
        renderInput={(params) => <TextField {...params} label={label} autoFocus={autoFocus} />}
        renderOption={({key, ...props}, tag, {selected}) => {
          const excluded = isExcluded(tag.id);
          return (
            <li key={key} {...props}>
              <Box
                sx={{
                  flexGrow: 1,
                  minWidth: 0,
                  ...(excluded && {color: "error.main", textDecoration: "line-through"}),
                }}
              >
                {tag.name}
              </Box>
              {selected && !excluded && <CheckIcon fontSize="small" color="primary" />}
              <Tooltip title={excluded ? "Убрать исключение" : "Исключить"}>
                <IconButton
                  size="small"
                  color={excluded ? "error" : "default"}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={(e) => {
                    e.stopPropagation();
                    toggleExcluded(tag.id);
                  }}
                >
                  <BlockIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </li>
          );
        }}
        renderValue={(tagValue, getItemProps) =>
          tagValue.map((tag, index) => {
            const excluded = isExcluded(tag.id);
            return (
              <Tooltip
                key={tag.id}
                title={excluded ? "Исключён — нажмите, чтобы включить" : "Нажмите, чтобы исключить"}
                disableInteractive
                enterDelay={400}
                enterNextDelay={400}
              >
                <Chip
                  label={tag.name}
                  size="small"
                  color={excluded ? "error" : "default"}
                  icon={excluded ? <BlockIcon /> : undefined}
                  {...getItemProps({index})}
                  key={tag.id}
                  onClick={() => switchMode(tag.id)}
                />
              </Tooltip>
            );
          })
        }
      />
    </TagsPaperContext.Provider>
  );
}

export default TagFilterAutocomplete;
