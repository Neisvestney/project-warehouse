import {type SortOrder} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";

export interface TableSortState<TSortBy extends string> {
  sortBy: TSortBy;
  sortOrder: SortOrder;
  handleSortClick: (column: TSortBy) => void;
}

export function useTableSort<TSortBy extends string>(
  columns: readonly {key: TSortBy; label: string}[],
  defaultSortBy: TSortBy,
): TableSortState<TSortBy> {
  const [sortBy, setSortBy] = useSyncedWithQueryState<TSortBy>(
    "sortBy",
    (q) => (q && columns.some((c) => c.key === q) ? (q as TSortBy) : defaultSortBy),
    (v) => (v === defaultSortBy ? null : v),
  );

  const [sortOrder, setSortOrder] = useSyncedWithQueryState<SortOrder>(
    "sortOrder",
    (q) => (q === "desc" ? "desc" : "asc"),
    (v) => (v === "asc" ? null : v),
  );

  const handleSortClick = (column: TSortBy) => {
    if (sortBy === column) {
      setSortOrder(sortOrder === "asc" ? "desc" : "asc");
    } else {
      setSortBy(column);
      setSortOrder("asc");
    }
  };

  return {sortBy, sortOrder, handleSortClick};
}
