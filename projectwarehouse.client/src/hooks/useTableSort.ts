import {type SortOrder} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

export interface TableSortState<TSortBy extends string> {
  sortBy: TSortBy;
  sortOrder: SortOrder;
  handleSortClick: (column: TSortBy) => void;
}

interface UseTableSortOptions {
  sortByParam?: string;
  sortOrderParam?: string;
  defaultSortOrder?: SortOrder;
  /** Adds a third click on the active column that drops the sort back to `defaultSortBy`. */
  clearable?: boolean;
}

export function useTableSort<TSortBy extends string>(
  columns: readonly {key: TSortBy; label: string}[],
  defaultSortBy: TSortBy,
  options?: UseTableSortOptions,
): TableSortState<TSortBy> {
  const sortByParam = options?.sortByParam ?? "sortBy";
  const sortOrderParam = options?.sortOrderParam ?? "sortOrder";

  const [sortBy, setSortBy] = useSyncedWithQueryState<TSortBy>(
    sortByParam,
    (q) => (q && columns.some((c) => c.key === q) ? (q as TSortBy) : defaultSortBy),
    (v) => (v === defaultSortBy ? null : v),
  );

  const [sortOrder, setSortOrder] = useSyncedWithQueryState<SortOrder>(
    sortOrderParam,
    (q) =>
      q === "desc" ||
      (options?.defaultSortOrder != null &&
        sortBy == defaultSortBy &&
        q == null &&
        options.defaultSortOrder == "desc")
        ? "desc"
        : "asc",
    (v) => (v === "asc" && options?.defaultSortOrder == null ? null : v),
  );

  const handleSortClick = (column: TSortBy) => {
    if (sortBy !== column) {
      setSortBy(column);
      setSortOrder("asc");
      return;
    }
    if (sortOrder === "asc") {
      setSortOrder("desc");
      return;
    }
    if (options?.clearable) {
      setSortBy(defaultSortBy);
      setSortOrder(options.defaultSortOrder ?? "asc");
      return;
    }
    setSortOrder("asc");
  };

  return {sortBy, sortOrder, handleSortClick};
}
