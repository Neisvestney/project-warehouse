import {useEffect, useState} from "react";
import {Autocomplete, Box, TextField, Typography, type SxProps, type Theme} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {ordersGetSuppliesOptions, ordersGetSupplyOptions} from "@/api/@tanstack/react-query.gen";
import type {MarketplaceSupplyDto} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";
import {isNotFoundError} from "@/utils/errorUtils";
import {formatSupplyDate, supplyName} from "./marketplaceSupplyUtils";

interface MarketplaceSupplySelectProps {
  value: string | null;
  onChange: (supplyId: string | null) => void;
  /** Narrows the search to one account's supplies. */
  marketplaceAccountId?: string | null;
  sx?: SxProps<Theme>;
}

const supplyLabel = (supply: MarketplaceSupplyDto) =>
  `${formatSupplyDate(supply)} · ${supplyName(supply)}`;

/** Server-searched supply picker, newest supplies first. */
function MarketplaceSupplySelect({
  value,
  onChange,
  marketplaceAccountId,
  sx,
}: MarketplaceSupplySelectProps) {
  const [inputValue, setInputValue] = useState("");
  // only typed text searches: the label a selection writes into the input would hide every other supply
  const [searchText, setSearchText] = useState("");
  const debouncedInput = useDebounce(searchText, 300);
  const [open, setOpen] = useState(false);

  const searchQuery = useQuery({
    ...ordersGetSuppliesOptions({
      query: {
        search: debouncedInput || undefined,
        marketplaceAccountId: marketplaceAccountId ?? undefined,
      },
    }),
    enabled: open,
  });

  // a deep link or a click in the table carries only the id
  const selectedQuery = useQuery({
    ...ordersGetSupplyOptions({path: {supplyId: value ?? ""}}),
    enabled: value !== null,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  // a link to a supply WMS does not know would leave an empty picker over an empty list
  const isUnknown = value !== null && isNotFoundError(selectedQuery.error);
  useEffect(() => {
    if (isUnknown) onChange(null);
  }, [isUnknown, onChange]);
  const selected = value !== null ? (selectedQuery.data ?? null) : null;

  const results = searchQuery.data ?? [];
  const options =
    selected && !results.some((s) => s.id === selected.id) ? [selected, ...results] : results;

  return (
    <Autocomplete
      sx={sx}
      size="small"
      open={open}
      onOpen={() => setOpen(true)}
      onClose={() => setOpen(false)}
      options={options}
      value={selected}
      onChange={(_, supply) => onChange(supply?.id ?? null)}
      inputValue={inputValue}
      onInputChange={(_, v, reason) => {
        setInputValue(v);
        setSearchText(reason === "input" ? v : "");
      }}
      getOptionLabel={supplyLabel}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterOptions={(x) => x}
      loading={searchQuery.isLoading || (value !== null && selectedQuery.isLoading)}
      noOptionsText="Поставки не найдены"
      renderInput={(params) => (
        <TextField
          {...params}
          label="Поставка"
          placeholder={value !== null && selectedQuery.isLoading ? "Загрузка…" : undefined}
        />
      )}
      renderOption={(props, supply) => (
        <li {...props} key={supply.id}>
          <Box sx={{display: "flex", alignItems: "baseline", gap: 1, width: "100%", minWidth: 0}}>
            <Box sx={{flex: 1, minWidth: 0}}>
              <Typography variant="body2">{formatSupplyDate(supply)}</Typography>
              <Typography variant="caption" color="text.secondary" sx={{display: "block"}}>
                {supplyName(supply)}
              </Typography>
            </Box>
            <Typography variant="caption" color="text.secondary" sx={{flexShrink: 0}}>
              {supply.marketplaceAccountName}
            </Typography>
          </Box>
        </li>
      )}
    />
  );
}

export default MarketplaceSupplySelect;
