import {useSyncedWithQueryAndStorageState} from "@/hooks/useSyncedWithQueryAndStorageState";

export interface TableColumnOption {
  key: string;
  label: string;
  defaultHidden?: boolean;
}

interface TableColumnsOptions {
  storageKey: string;
  queryParam?: string;
}

export function useTableColumns<T extends TableColumnOption>(
  columns: readonly T[],
  {storageKey, queryParam = "columns"}: TableColumnsOptions,
) {
  const optionalColumns = columns.filter((column) => column.defaultHidden);
  const [enabledColumns, setEnabledColumns] = useSyncedWithQueryAndStorageState<string[]>(
    queryParam,
    storageKey,
    (q) =>
      q?.split(",").filter((key) => optionalColumns.some((column) => column.key === key)) ?? [],
    (value) => value.join(","),
  );
  const visibleColumns = columns.filter(
    (column) => !column.defaultHidden || enabledColumns.includes(column.key),
  );

  return {visibleColumns, optionalColumns, enabledColumns, setEnabledColumns};
}
