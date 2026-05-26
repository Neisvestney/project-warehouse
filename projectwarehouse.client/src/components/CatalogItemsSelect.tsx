import React, {useEffect, useMemo, useRef, useState} from "react";
import type {AutocompleteProps, TextFieldProps} from "@mui/material";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {catalogGetAllOptions, catalogGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import type {CatalogItemDto, CatalogItemSummaryDto, CatalogItemType} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";

function toSummary(dto: CatalogItemDto): CatalogItemSummaryDto {
  return {
    id: dto.id,
    type: dto.type,
    name: dto.name,
    fullName: dto.fullName,
    article: dto.article,
    barcode: dto.barcode,
    isArchived: dto.isArchived,
    tags: dto.tags,
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
  AutocompleteProps<CatalogItemSummaryDto, true, false, false>
> {
  label?: string;
  multiple: true;
  value: CatalogItemSummaryDto[];
  onChange: (value: CatalogItemSummaryDto[]) => void;
  types?: CatalogItemType[];
}

interface CatalogItemsSelectSingleProps extends OmitControlled<
  AutocompleteProps<CatalogItemSummaryDto, false, false, false>
> {
  label?: string;
  multiple?: false;
  value: string | null;
  onChange: (id: string | null) => void;
  /** Called when the DTO for the current value is resolved — on initial load and on every change. */
  onDtoChange?: (dto: CatalogItemSummaryDto | null) => void;
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

function filterByTypes(
  items: CatalogItemSummaryDto[],
  types?: CatalogItemType[],
): CatalogItemSummaryDto[] {
  if (!types || types.length === 0) return items;
  return items.filter((item) => types.includes(item.type));
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
    catalogGetAllOptions({query: {searchString: debouncedInput || undefined}}),
  );

  const options = useMemo(() => {
    const results = filterByTypes(searchQuery.data?.items ?? [], types);
    const seen = new Set(results.map((item) => item.id));
    return [...results, ...value.filter((item) => !seen.has(item.id))];
  }, [searchQuery.data, value, types]);

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
    catalogGetAllOptions({query: {searchString: debouncedInput || undefined}}),
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

  const fetchedSummary = useMemo(
    () => (getByIdQuery.data ? toSummary(getByIdQuery.data) : undefined),
    [getByIdQuery.data],
  );

  useEffect(() => {
    if (value === null) {
      onDtoChangeRef.current?.(null);
      return;
    }
    if (fetchedSummary) {
      onDtoChangeRef.current?.(fetchedSummary);
    }
  }, [value, fetchedSummary]);

  const options = useMemo(() => {
    const results = filterByTypes(searchQuery.data?.items ?? [], types);
    const seen = new Set(results.map((item) => item.id));
    const extra = fetchedSummary && !seen.has(fetchedSummary.id) ? [fetchedSummary] : [];
    return [...results, ...extra];
  }, [searchQuery.data, fetchedSummary, types]);

  return (
    <Autocomplete
      {...autocompleteProps}
      options={options}
      value={fetchedSummary ?? null}
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
    />
  );
}

export default CatalogItemsSelect;
