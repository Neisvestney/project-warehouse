import {useMemo, useState} from "react";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useMutation, useQuery} from "@tanstack/react-query";
import {
  createDocumentTag,
  type DocumentTag,
  type DocumentTagKind,
  documentTagsQueryOptions,
} from "@/components/tags/documentTags";
import {useDebounce} from "@/hooks/useDebounce";

const NEW_TAG_PREFIX = "__new__:";

type DocumentTagsAutocompleteProps = {
  kind: DocumentTagKind;
  value: DocumentTag[];
  onChange: (v: DocumentTag[]) => void;
  disabled?: boolean;
};

/** Free-solo multiselect over the tags of one document type — typing a new name creates the tag on selection. */
function DocumentTagsAutocomplete({
  kind,
  value,
  onChange,
  disabled,
}: DocumentTagsAutocompleteProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);
  const tagsQuery = useQuery(documentTagsQueryOptions(kind, debouncedInput || undefined));
  const createMutation = useMutation({mutationFn: (name: string) => createDocumentTag(kind, name)});

  const options = useMemo(() => {
    const results = tagsQuery.data ?? [];
    const seen = new Set(results.map((t) => t.id));
    return [...results, ...value.filter((t) => !seen.has(t.id))];
  }, [tagsQuery.data, value]);

  const handleChange = async (_: React.SyntheticEvent, newValue: (DocumentTag | string)[]) => {
    const resolved: DocumentTag[] = [];
    for (const item of newValue) {
      if (typeof item === "string") {
        const trimmed = item.trim();
        if (!trimmed) continue;
        resolved.push(await createMutation.mutateAsync(trimmed));
      } else if (item.id.startsWith(NEW_TAG_PREFIX)) {
        resolved.push(await createMutation.mutateAsync(item.id.slice(NEW_TAG_PREFIX.length)));
      } else {
        resolved.push(item);
      }
    }
    onChange(resolved);
  };

  return (
    <Autocomplete
      multiple
      freeSolo
      options={options}
      value={value}
      onChange={handleChange}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={(t) => (typeof t === "string" ? t : t.name)}
      isOptionEqualToValue={(o, v) =>
        typeof o !== "string" && typeof v !== "string" && o.id === v.id
      }
      filterSelectedOptions
      filterOptions={(x, params) => {
        const trimmed = params.inputValue.trim();
        const alreadyExists = x.some(
          (o) => typeof o != "string" && o.name.toLowerCase() === trimmed.toLowerCase(),
        );
        if (trimmed && !alreadyExists) {
          return [...x, {id: `${NEW_TAG_PREFIX}${trimmed}`, name: `Создать «${trimmed}»`}];
        }
        return x;
      }}
      loading={tagsQuery.isLoading || createMutation.isPending}
      disabled={disabled || createMutation.isPending}
      size="small"
      renderInput={(params) => <TextField {...params} label="Теги" />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => {
          const tag = option as DocumentTag;
          return <Chip label={tag.name} {...getItemProps({index})} key={tag.id} size="small" />;
        })
      }
    />
  );
}

export default DocumentTagsAutocomplete;
