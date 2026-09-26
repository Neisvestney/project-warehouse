import {useState} from "react";
import {
  Autocomplete,
  Box,
  Chip,
  InputAdornment,
  TextField,
  Typography,
  type SxProps,
  type Theme,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import {useQuery} from "@tanstack/react-query";
import {catalogGetForSelectOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemSelectDto} from "@/api/types.gen";
import {useCatalogItemsByIds} from "@/hooks/useCatalogItemsByIds";
import {useDebounce} from "@/hooks/useDebounce";
import {CatalogItemOptionContent} from "@/components/CatalogItemsSelect";

interface TextOption {
  kind: "text";
}

type Option = TextOption | CatalogItemSelectDto;

const TEXT_OPTION: TextOption = {kind: "text"};

const isTextOption = (option: Option | string): option is TextOption =>
  typeof option !== "string" && "kind" in option;

interface SearchWithItemsInputProps {
  text: string;
  onTextChange: (value: string) => void;
  itemIds: string[];
  onItemIdsChange: (ids: string[]) => void;
  label?: string;
  sx?: SxProps<Theme>;
}

/**
 * Free-text search with catalog items as chips in the same field. The first suggestion always searches by
 * the typed text; picking a catalog item turns it into a chip and clears the text.
 */
function SearchWithItemsInput({
  text,
  onTextChange,
  itemIds,
  onItemIdsChange,
  label = "Поиск",
  sx,
}: SearchWithItemsInputProps) {
  const [open, setOpen] = useState(false);
  const debouncedText = useDebounce(text.trim(), 300);

  const searchQuery = useQuery({
    ...catalogGetForSelectOptions({query: {searchString: debouncedText}}),
    enabled: debouncedText.length > 0,
  });

  // a picked option is shown right away instead of waiting for the by-ids lookup to catch up
  const [picked, setPicked] = useState<Map<string, CatalogItemSelectDto>>(() => new Map());
  const known = useCatalogItemsByIds(itemIds);
  const selected = itemIds.flatMap((id) => known.items.get(id) ?? picked.get(id) ?? []);

  const options: Option[] = text.trim() ? [TEXT_OPTION, ...(searchQuery.data ?? [])] : [];

  return (
    <Autocomplete<Option, true, false, true>
      multiple
      freeSolo
      autoHighlight
      filterSelectedOptions
      sx={sx}
      open={open && options.length > 0}
      onOpen={() => setOpen(true)}
      onClose={() => setOpen(false)}
      options={options}
      value={selected}
      inputValue={text}
      onInputChange={(_, value, reason) => {
        // "reset" and "blur" would wipe or rewrite the text after a pick; the text changes only when typed or cleared
        if (reason === "input" || reason === "clear") onTextChange(value);
      }}
      onChange={(_, __, reason, details) => {
        if (reason === "clear") {
          onItemIdsChange([]);
          onTextChange("");
          return;
        }
        const option = details?.option;
        if (!option || typeof option === "string" || isTextOption(option)) return;
        if (reason === "selectOption") {
          setPicked((prev) => new Map(prev).set(option.id, option));
          onItemIdsChange([...itemIds, option.id]);
          onTextChange("");
        } else if (reason === "removeOption") {
          onItemIdsChange(itemIds.filter((id) => id !== option.id));
        }
      }}
      getOptionLabel={(option) =>
        typeof option === "string" ? option : isTextOption(option) ? text : option.fullName
      }
      getOptionKey={(option) =>
        typeof option === "string" ? option : isTextOption(option) ? "__text" : option.id
      }
      isOptionEqualToValue={(o, v) =>
        !isTextOption(o) && typeof v !== "string" && !isTextOption(v) && o.id === v.id
      }
      filterOptions={(x) => x}
      loading={searchQuery.isFetching}
      renderOption={({key, ...props}, option) => (
        <li key={key} {...props}>
          {isTextOption(option) ? (
            <Box sx={{display: "flex", alignItems: "center", gap: 1, minWidth: 0}}>
              <SearchIcon fontSize="small" sx={{color: "text.secondary"}} />
              <Typography variant="body2" noWrap>
                Искать «{text.trim()}»
              </Typography>
            </Box>
          ) : (
            <CatalogItemOptionContent item={option} />
          )}
        </li>
      )}
      renderValue={(value, getItemProps) =>
        value.map((option, index) =>
          typeof option === "string" || isTextOption(option) ? null : (
            <Chip
              {...getItemProps({index})}
              key={option.id}
              label={option.fullName}
              size="small"
              sx={{borderRadius: 1, maxWidth: 240}}
            />
          ),
        )
      }
      renderInput={(params) => (
        <TextField
          {...params}
          size="small"
          label={label}
          slotProps={{
            ...params.slotProps,
            input: {
              ...params.slotProps?.input,
              startAdornment: (
                <>
                  <InputAdornment position="start">
                    <SearchIcon />
                  </InputAdornment>
                  {params.slotProps?.input?.startAdornment}
                </>
              ),
            },
          }}
        />
      )}
    />
  );
}

export default SearchWithItemsInput;
