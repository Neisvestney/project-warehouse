/** Tag condition of a filter: any of `include` and none of `exclude`. A tag id sits in one list at most. */
export type TagFilter = {
  include: string[];
  exclude: string[];
};

export const EMPTY_TAG_FILTER: TagFilter = {include: [], exclude: []};

const EXCLUDE_PREFIX = "-";

export function isTagFilterActive(filter: TagFilter): boolean {
  return filter.include.length > 0 || filter.exclude.length > 0;
}

/** URL form `a,b,-c`: a plain id is included, a `-`-prefixed one excluded. */
export function parseTagFilter(q: string | null): TagFilter {
  if (!q) return EMPTY_TAG_FILTER;
  const include: string[] = [];
  const exclude: string[] = [];
  for (const part of q.split(",")) {
    if (part.startsWith(EXCLUDE_PREFIX)) {
      const id = part.slice(EXCLUDE_PREFIX.length);
      if (id) exclude.push(id);
    } else if (part) {
      include.push(part);
    }
  }
  return {include, exclude};
}

export function serializeTagFilter(filter: TagFilter): string | null {
  const parts = [...filter.include, ...filter.exclude.map((id) => EXCLUDE_PREFIX + id)];
  return parts.length > 0 ? parts.join(",") : null;
}
