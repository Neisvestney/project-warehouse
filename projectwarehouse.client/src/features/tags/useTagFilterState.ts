import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {parseTagFilter, serializeTagFilter, type TagFilter} from "./tagFilter";

/** Tag filter kept in one URL param, see `parseTagFilter` for the format. */
export function useTagFilterState(key = "tags"): [TagFilter, (value: TagFilter) => void] {
  return useSyncedWithQueryState<TagFilter>(key, parseTagFilter, serializeTagFilter);
}
