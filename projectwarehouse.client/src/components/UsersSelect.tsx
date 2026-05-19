import {useMemo, useState} from "react";
import type {AutocompleteProps} from "@mui/material";
import {Autocomplete, Chip, TextField} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {usersGetAllOptions} from "@/api/@tanstack/react-query.gen";
import type {UserDetailDto} from "@/api/types.gen";
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

interface UsersSelectProps extends OmitControlled<
  AutocompleteProps<UserDetailDto, true, false, false>
> {
  label?: string;
  value: string[];
  onChange: (ids: string[]) => void;
  warehouseId?: string | null;
}

function getUserLabel(user: UserDetailDto): string {
  const name = [user.firstName, user.lastName].filter(Boolean).join(" ");
  return name ? `${user.username} (${name})` : user.username;
}

function UsersSelect({
  value,
  onChange,
  warehouseId,
  label = "Назначенные",
  ...autocompleteProps
}: UsersSelectProps) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);

  const searchQuery = useQuery(
    usersGetAllOptions({
      query: {
        searchString: debouncedInput || undefined,
        warehouse: warehouseId ?? undefined,
      },
    }),
  );

  const allUsers = useMemo(() => searchQuery.data?.items ?? [], [searchQuery.data]);

  // Keep a growing map of every user we've seen so selected chips survive search changes
  const [knownUsersSnapshot, setKnownUsersSnapshot] = useState<UserDetailDto[]>([]);
  const [prevAllUsers, setPrevAllUsers] = useState<UserDetailDto[]>([]);
  if (allUsers !== prevAllUsers) {
    setPrevAllUsers(allUsers);
    setKnownUsersSnapshot((prev) => {
      const merged = new Map(prev.map((u) => [u.id, u]));
      allUsers.forEach((u) => merged.set(u.id, u));
      return Array.from(merged.values());
    });
  }

  const knownUsersMap = useMemo(
    () => new Map(knownUsersSnapshot.map((u) => [u.id, u])),
    [knownUsersSnapshot],
  );

  const selectedUsers = useMemo(
    () =>
      value.map((id) => knownUsersMap.get(id)).filter((u): u is UserDetailDto => u !== undefined),
    [value, knownUsersMap],
  );

  const options = useMemo(() => {
    const seen = new Set(allUsers.map((u) => u.id));
    return [...allUsers, ...selectedUsers.filter((u) => !seen.has(u.id))];
  }, [allUsers, selectedUsers]);

  return (
    <Autocomplete
      {...autocompleteProps}
      multiple
      options={options}
      value={selectedUsers}
      onChange={(_, v) => onChange(v.map((u) => u.id))}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={getUserLabel}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      filterSelectedOptions
      filterOptions={(x) => x}
      loading={searchQuery.isLoading}
      renderInput={(params) => <TextField {...params} label={label} />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => (
          <Chip label={option.username} {...getItemProps({index})} key={option.id} size="small" />
        ))
      }
    />
  );
}

export default UsersSelect;
