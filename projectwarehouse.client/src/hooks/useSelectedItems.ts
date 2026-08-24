import {useState} from "react";

/**
 * Keeps full selected rows across page switches and filter changes,
 * so bulk actions still see items that are no longer on the visible page.
 * `freshItems` - the currently fetched page; matching selections are refreshed from it.
 */
export function useSelectedItems<T>(getId: (item: T) => string, freshItems?: T[]) {
  const [selectedItems, setSelectedItems] = useState<T[]>([]);
  const [lastFresh, setLastFresh] = useState(freshItems);

  const pageItems = freshItems ?? [];

  // a row selected pages ago would otherwise be acted on as the snapshot taken when it was ticked
  if (freshItems !== lastFresh) {
    setLastFresh(freshItems);
    if (pageItems.length > 0 && selectedItems.length > 0) {
      const fresh = new Map(pageItems.map((i) => [getId(i), i]));
      const next = selectedItems.map((i) => fresh.get(getId(i)) ?? i);
      if (next.some((item, idx) => item !== selectedItems[idx])) setSelectedItems(next);
    }
  }

  const selectedIds = new Set(selectedItems.map(getId));
  const allPageSelected = pageItems.length > 0 && pageItems.every((i) => selectedIds.has(getId(i)));
  const somePageSelected = pageItems.some((i) => selectedIds.has(getId(i)));

  function isSelected(id: string) {
    return selectedIds.has(id);
  }

  function toggle(item: T) {
    const id = getId(item);
    setSelectedItems((prev) =>
      prev.some((i) => getId(i) === id) ? prev.filter((i) => getId(i) !== id) : [...prev, item],
    );
  }

  /** Selects the whole current page, or clears it when it is already fully selected. */
  function toggleAll() {
    const pageIds = new Set(pageItems.map(getId));
    setSelectedItems((prev) => {
      const rest = prev.filter((i) => !pageIds.has(getId(i)));
      const allSelected = pageItems.length > 0 && prev.length - rest.length === pageItems.length;
      return allSelected ? rest : [...rest, ...pageItems];
    });
  }

  function removeIds(ids: string[]) {
    const removed = new Set(ids);
    setSelectedItems((prev) => prev.filter((i) => !removed.has(getId(i))));
  }

  function clear() {
    setSelectedItems([]);
  }

  return {
    selectedItems,
    selectedIds,
    isSelected,
    allPageSelected,
    somePageSelected,
    toggle,
    toggleAll,
    removeIds,
    clear,
  };
}
