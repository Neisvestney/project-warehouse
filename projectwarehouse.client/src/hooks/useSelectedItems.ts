import {useState} from "react";

/**
 * Keeps full selected rows across page switches and filter changes,
 * so bulk actions still see items that are no longer on the visible page.
 * `freshItems` - the currently fetched page; matching selections are refreshed from it.
 */
export function useSelectedItems<T>(getId: (item: T) => string, freshItems?: T[]) {
  const [selectedItems, setSelectedItems] = useState<T[]>([]);
  const [lastFresh, setLastFresh] = useState(freshItems);
  const [anchorId, setAnchorId] = useState<string | null>(null);

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

  /**
   * With `extendRange` (Shift+click) every page row between the previously toggled one and `item`
   * takes the state `item` switches to; falls back to a single toggle when that row is not on the page.
   */
  function toggle(item: T, extendRange = false) {
    const id = getId(item);
    const index = pageItems.findIndex((i) => getId(i) === id);
    const anchorIndex =
      extendRange && anchorId != null ? pageItems.findIndex((i) => getId(i) === anchorId) : -1;
    const range =
      index >= 0 && anchorIndex >= 0
        ? pageItems.slice(Math.min(index, anchorIndex), Math.max(index, anchorIndex) + 1)
        : [item];
    const rangeIds = new Set(range.map(getId));

    setAnchorId(id);
    setSelectedItems((prev) => {
      const select = !prev.some((i) => getId(i) === id);
      const rest = prev.filter((i) => !rangeIds.has(getId(i)));
      return select ? [...rest, ...range] : rest;
    });
  }

  /** Selects the whole current page, or clears it when it is already fully selected. */
  function toggleAll() {
    const pageIds = new Set(pageItems.map(getId));
    setAnchorId(null);
    setSelectedItems((prev) => {
      const rest = prev.filter((i) => !pageIds.has(getId(i)));
      const allSelected = pageItems.length > 0 && prev.length - rest.length === pageItems.length;
      return allSelected ? rest : [...rest, ...pageItems];
    });
  }

  function removeIds(ids: string[]) {
    const removed = new Set(ids);
    setAnchorId(null);
    setSelectedItems((prev) => prev.filter((i) => !removed.has(getId(i))));
  }

  function clear() {
    setAnchorId(null);
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
