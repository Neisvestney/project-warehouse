import React, {useEffect, useMemo, useRef, useState} from "react";
import type {AutocompleteProps, TextFieldProps} from "@mui/material";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {warehousesGetAllOptions, warehousesGetByIdOptions} from "@/api/@tanstack/react-query.gen";
import type {WarehouseDto, WarehouseSummaryDto} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";

function toSummary(dto: WarehouseDto): WarehouseSummaryDto {
  return {
    id: dto.id,
    name: dto.name,
    width: dto.width,
    height: dto.height,
    storagePlaceCount: dto.storagePlaces.length,
    totalItemsCount: dto.totalItemsCount,
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

interface WarehousesSelectMultiProps extends OmitControlled<
  AutocompleteProps<WarehouseSummaryDto, true, false, false>
> {
  label?: string;
  multiple: true;
  value: WarehouseSummaryDto[];
  onChange: (value: WarehouseSummaryDto[]) => void;
}

interface WarehousesSelectSingleProps extends OmitControlled<
  AutocompleteProps<WarehouseSummaryDto, false, false, false>
> {
  label?: string;
  multiple?: false;
  value: string | null;
  onChange: (id: string | null) => void;
  /** Called when the DTO for the current value is resolved — on initial load and on every change. */
  onDtoChange?: (dto: WarehouseSummaryDto | null) => void;
  textFieldProps?: Partial<TextFieldProps>;
}

export type WarehousesSelectProps = WarehousesSelectMultiProps | WarehousesSelectSingleProps;

function WarehousesSelect(props: WarehousesSelectMultiProps): React.ReactElement;
function WarehousesSelect(props: WarehousesSelectSingleProps): React.ReactElement;
function WarehousesSelect(props: WarehousesSelectProps): React.ReactElement {
  if (props.multiple) {
    return <MultiSelect {...props} />;
  }
  return <SingleSelect {...props} />;
}

function MultiSelect({
  value,
  onChange,
  label = "Склады",
  ...autocompleteProps
}: WarehousesSelectMultiProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    warehousesGetAllOptions({query: {searchString: debouncedInput || undefined}}),
  );

  const options = useMemo(() => {
    const results = searchQuery.data?.items ?? [];
    const seen = new Set(results.map((w) => w.id));
    return [...results, ...value.filter((w) => !seen.has(w.id))];
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
      getOptionLabel={(w) => w.name}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterSelectedOptions
      filterOptions={(x) => x}
      loading={searchQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => (
          <Chip label={option.name} {...getItemProps({index})} key={option.id} size="small" />
        ))
      }
    />
  );
}

function SingleSelect({
  value,
  onChange,
  onDtoChange,
  label = "Склад",
  textFieldProps,
  ...autocompleteProps
}: WarehousesSelectSingleProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    warehousesGetAllOptions({query: {searchString: debouncedInput || undefined}}),
  );

  const getByIdQuery = useQuery({
    ...warehousesGetByIdOptions({path: {id: value!}}),
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
    const results = searchQuery.data?.items ?? [];
    const seen = new Set(results.map((w) => w.id));
    const extra = fetchedSummary && !seen.has(fetchedSummary.id) ? [fetchedSummary] : [];
    return [...results, ...extra];
  }, [searchQuery.data, fetchedSummary]);

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
      getOptionLabel={(w) => w.name}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterOptions={(x) => x}
      loading={searchQuery.isLoading || getByIdQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} {...textFieldProps} />}
    />
  );
}

export default WarehousesSelect;
