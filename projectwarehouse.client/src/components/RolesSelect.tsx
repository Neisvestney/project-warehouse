import React, {useEffect, useMemo, useRef, useState} from "react";
import type {AutocompleteProps} from "@mui/material";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {rolesGetByIdOptions, rolesSearchOptions} from "@/api/@tanstack/react-query.gen";
import type {RoleDto} from "@/api/types.gen";
import {useDebounce} from "@/hooks/useDebounce";

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

interface RolesSelectMultiProps extends OmitControlled<
  AutocompleteProps<RoleDto, true, false, false>
> {
  label?: string;
  multiple: true;
  value: RoleDto[];
  onChange: (value: RoleDto[]) => void;
}

interface RolesSelectSingleProps extends OmitControlled<
  AutocompleteProps<RoleDto, false, false, false>
> {
  label?: string;
  multiple?: false;
  value: string | null;
  onChange: (id: string | null) => void;
  /** Called when the DTO for the current value is resolved — on initial load and on every change. */
  onDtoChange?: (dto: RoleDto | null) => void;
}

export type RolesSelectProps = RolesSelectMultiProps | RolesSelectSingleProps;

function RolesSelect(props: RolesSelectMultiProps): React.ReactElement;
function RolesSelect(props: RolesSelectSingleProps): React.ReactElement;
function RolesSelect(props: RolesSelectProps): React.ReactElement {
  if (props.multiple) {
    return <MultiSelect {...props} />;
  }
  return <SingleSelect {...props} />;
}

function MultiSelect({
  value,
  onChange,
  label = "Роли",
  ...autocompleteProps
}: RolesSelectMultiProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    rolesSearchOptions({query: {searchString: debouncedInput || undefined}}),
  );

  const options = useMemo(() => {
    const results = searchQuery.data ?? [];
    const seen = new Set(results.map((r) => r.id));
    return [...results, ...value.filter((r) => !seen.has(r.id))];
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
      getOptionLabel={(r) => r.name}
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
  label = "Роль",
  ...autocompleteProps
}: RolesSelectSingleProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    rolesSearchOptions({query: {searchString: debouncedInput || undefined}}),
  );

  const getByIdQuery = useQuery({
    ...rolesGetByIdOptions({path: {id: value!}}),
    enabled: value !== null,
    meta: {suppressGlobalError: true},
  });

  const onDtoChangeRef = useRef(onDtoChange);
  useEffect(() => {
    onDtoChangeRef.current = onDtoChange;
  });

  useEffect(() => {
    if (value === null) {
      onDtoChangeRef.current?.(null);
      return;
    }
    if (getByIdQuery.data) {
      onDtoChangeRef.current?.(getByIdQuery.data);
    }
  }, [value, getByIdQuery.data]);

  const options = useMemo(() => {
    const results = searchQuery.data ?? [];
    const seen = new Set(results.map((r) => r.id));
    const fetched = getByIdQuery.data;
    const extra = fetched && !seen.has(fetched.id) ? [fetched] : [];
    return [...results, ...extra];
  }, [searchQuery.data, getByIdQuery.data]);

  return (
    <Autocomplete
      {...autocompleteProps}
      options={options}
      value={getByIdQuery.data ?? null}
      onChange={(_, dto) => {
        onChange(dto?.id ?? null);
        onDtoChangeRef.current?.(dto);
      }}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={(r) => r.name}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterOptions={(x) => x}
      loading={searchQuery.isLoading || getByIdQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} />}
    />
  );
}

export default RolesSelect;
