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
  const optionalColumns = columns.filter((column) => column.defaultHidden !== undefined);
  const defaultColumns = optionalColumns
    .filter((column) => !column.defaultHidden)
    .map((column) => column.key);
  const [selection, setSelection] = useSyncedWithQueryAndStorageState<string | null>(
    queryParam,
    storageKey,
    (q) => q,
    (value) => value ?? "",
  );
  const keys = selection?.split(",") ?? [];
  const enabledColumns = [
    ...new Set([
      ...keys.filter((key) => optionalColumns.some((column) => column.key === key)),
      ...(keys.includes("_configured") ? [] : defaultColumns),
    ]),
  ];
  const setEnabledColumns = (value: string[]) =>
    setSelection([...value, ...(defaultColumns.length ? ["_configured"] : [])].join(","));
  const visibleColumns = columns.filter(
    (column) => column.defaultHidden === undefined || enabledColumns.includes(column.key),
  );

  return {visibleColumns, optionalColumns, enabledColumns, setEnabledColumns};
}
