import {useMemo, useState} from "react";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useMutation, useQuery} from "@tanstack/react-query";
import {receiptsCreateTagMutation, receiptsGetTagsOptions} from "@/api/@tanstack/react-query.gen";
import type {ReceiptTagDto} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";

const NEW_TAG_PREFIX = "__new__:";

type ReceiptTagsAutocompleteProps = {
  value: ReceiptTagDto[];
  onChange: (v: ReceiptTagDto[]) => void;
  disabled?: boolean;
};

/** Free-solo multiselect over receipt tags — typing a new name creates the tag on selection. */
function ReceiptTagsAutocomplete({value, onChange, disabled}: ReceiptTagsAutocompleteProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);
  const tagsQuery = useQuery(
    receiptsGetTagsOptions({query: {search: debouncedInput || undefined}}),
  );
  const createMutation = useMutation(receiptsCreateTagMutation());

  const options = useMemo(() => {
    const results = tagsQuery.data ?? [];
    const seen = new Set(results.map((t) => t.id));
    return [...results, ...value.filter((t) => !seen.has(t.id))];
  }, [tagsQuery.data, value]);

  const handleChange = async (_: React.SyntheticEvent, newValue: (ReceiptTagDto | string)[]) => {
    const resolved: ReceiptTagDto[] = [];
    for (const item of newValue) {
      if (typeof item === "string") {
        const trimmed = item.trim();
        if (!trimmed) continue;
        const created = await createMutation.mutateAsync({body: {name: trimmed}});
        resolved.push(created);
      } else if (item.id.startsWith(NEW_TAG_PREFIX)) {
        const name = item.id.slice(NEW_TAG_PREFIX.length);
        const created = await createMutation.mutateAsync({body: {name}});
        resolved.push(created);
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
      getOptionLabel={(t) => (typeof t === "string" ? t : (t as ReceiptTagDto).name)}
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
          return [
            ...x,
            {id: `${NEW_TAG_PREFIX}${trimmed}`, name: `Создать «${trimmed}»`} as ReceiptTagDto,
          ];
        }
        return x;
      }}
      loading={tagsQuery.isLoading || createMutation.isPending}
      disabled={disabled || createMutation.isPending}
      size="small"
      renderInput={(params) => <TextField {...params} label="Теги" />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => {
          const tag = option as ReceiptTagDto;
          return <Chip label={tag.name} {...getItemProps({index})} key={tag.id} size="small" />;
        })
      }
    />
  );
}

export default ReceiptTagsAutocomplete;
