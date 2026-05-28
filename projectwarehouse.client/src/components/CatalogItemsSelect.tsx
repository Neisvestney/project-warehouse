import React, {useEffect, useMemo, useRef, useState} from "react";
import type {AutocompleteProps, TextFieldProps} from "@mui/material";
import {Autocomplete, Box, Chip, TextField, Typography} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {catalogGetByIdOptions, catalogGetForSelectOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemDto, CatalogItemSelectDto, CatalogItemType} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";

function toSelectDto(dto: CatalogItemDto): CatalogItemSelectDto {
  return {
    id: dto.id,
    type: dto.type,
    name: dto.name,
    fullName: dto.fullName,
    article: dto.article,
  };
}

type OmitControlled<T> = Omit<
  T,
  | "value"
  | "onChange"
  | "options"
  | "multiple"
  | "getOptionLabel"
  | "isOptionEqualToValue"
  | "filterSelectedOptions"
  | "filterOptions"
  | "loading"
  | "renderInput"
  | "renderValue"
  | "inputValue"
  | "onInputChange"
>;

interface CatalogItemsSelectMultiProps extends OmitControlled<
  AutocompleteProps<CatalogItemSelectDto, true, false, false>
> {
  label?: string;
  multiple: true;
  value: CatalogItemSelectDto[];
  onChange: (value: CatalogItemSelectDto[]) => void;
  types?: CatalogItemType[];
}

interface CatalogItemsSelectSingleProps extends OmitControlled<
  AutocompleteProps<CatalogItemSelectDto, false, false, false>
> {
  label?: string;
  multiple?: false;
  value: string | null;
  onChange: (id: string | null) => void;
  /** Called when the DTO for the current value is resolved — on initial load and on every change. */
  onDtoChange?: (dto: CatalogItemSelectDto | null) => void;
  textFieldProps?: Partial<TextFieldProps>;
  types?: CatalogItemType[];
}

export type CatalogItemsSelectProps = CatalogItemsSelectMultiProps | CatalogItemsSelectSingleProps;

function CatalogItemsSelect(props: CatalogItemsSelectMultiProps): React.ReactElement;
function CatalogItemsSelect(props: CatalogItemsSelectSingleProps): React.ReactElement;
function CatalogItemsSelect(props: CatalogItemsSelectProps): React.ReactElement {
  if (props.multiple) {
    return <MultiSelect {...(props as CatalogItemsSelectMultiProps)} />;
  }
  return <SingleSelect {...(props as CatalogItemsSelectSingleProps)} />;
}

function OptionContent({item}: {item: CatalogItemSelectDto}) {
  return (
    <Box sx={{display: "flex", alignItems: "center", gap: 1, width: "100%", minWidth: 0}}>
      <CatalogItemTypeChip type={item.type} />
      <Typography variant="body2" noWrap sx={{flex: 1}}>
        {item.fullName}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{flexShrink: 0}}>
        {item.article}
      </Typography>
    </Box>
  );
}

function MultiSelect({
  value,
  onChange,
  label = "Позиции каталога",
  types,
  ...autocompleteProps
}: CatalogItemsSelectMultiProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    catalogGetForSelectOptions({
      query: {searchString: debouncedInput || undefined, types: types},
    }),
  );

  const options = useMemo(() => {
    const results = searchQuery.data ?? [];
    const seen = new Set(results.map((item) => item.id));
    return [...results, ...value.filter((item) => !seen.has(item.id))];
  }, [searchQuery.data, value]);

  return (
    <Autocomplete
      {...autocompleteProps}
      multiple
      options={options}
      value={value}
      onChange={(_, v) => onChange(v)}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={(item) => item.fullName}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterSelectedOptions
      filterOptions={(x) => x}
      loading={searchQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} />}
      renderOption={(props, option) => (
        <li {...props} key={option.id}>
          <OptionContent item={option} />
        </li>
      )}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => (
          <Chip label={option.fullName} {...getItemProps({index})} key={option.id} size="small" />
        ))
      }
    />
  );
}

function SingleSelect({
  value,
  onChange,
  onDtoChange,
  label = "Позиция каталога",
  types,
  textFieldProps,
  ...autocompleteProps
}: CatalogItemsSelectSingleProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    catalogGetForSelectOptions({
      query: {searchString: debouncedInput || undefined, types: types},
    }),
  );

  const getByIdQuery = useQuery({
    ...catalogGetByIdOptions({path: {id: value!}}),
    enabled: value !== null,
    meta: {suppressGlobalError: true},
  });

  const onDtoChangeRef = useRef(onDtoChange);
  useEffect(() => {
    onDtoChangeRef.current = onDtoChange;
  });

  const fetchedSelectDto = useMemo(
    () => (getByIdQuery.data ? toSelectDto(getByIdQuery.data) : undefined),
    [getByIdQuery.data],
  );

  useEffect(() => {
    if (value === null) {
      onDtoChangeRef.current?.(null);
      return;
    }
    if (fetchedSelectDto) {
      onDtoChangeRef.current?.(fetchedSelectDto);
    }
  }, [value, fetchedSelectDto]);

  const options = useMemo(() => {
    const results = searchQuery.data ?? [];
    const seen = new Set(results.map((item) => item.id));
    const extra = fetchedSelectDto && !seen.has(fetchedSelectDto.id) ? [fetchedSelectDto] : [];
    return [...results, ...extra];
  }, [searchQuery.data, fetchedSelectDto]);

  return (
    <Autocomplete
      {...autocompleteProps}
      options={options}
      value={fetchedSelectDto ?? null}
      onChange={(_, dto) => {
        onChange(dto?.id ?? null);
        onDtoChangeRef.current?.(dto);
      }}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={(item) => item.fullName}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterOptions={(x) => x}
      loading={searchQuery.isLoading || getByIdQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} {...textFieldProps} />}
      renderOption={(props, option) => (
        <li {...props} key={option.id}>
          <OptionContent item={option} />
        </li>
      )}
    />
  );
}

export default CatalogItemsSelect;
