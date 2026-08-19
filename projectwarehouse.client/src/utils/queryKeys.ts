import type {QueryKey} from "@tanstack/react-query";

/**
 * Generated query keys are a single object `[{_id, baseUrl, path?, query?}]`, so an invalidation
 * filter built from a subset of those fields partially matches every variant of the operation —
 * including paginated queries whose `query` differs per page.
 */
export function byOperation(
  operationId: string,
  match?: {path?: Record<string, unknown>; query?: Record<string, unknown>},
): QueryKey {
  return [{_id: operationId, ...match}];
}
