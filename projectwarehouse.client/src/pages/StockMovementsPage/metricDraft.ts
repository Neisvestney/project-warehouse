import type {StockMovementMetricDto} from "@/api/types.gen";

/**
 * A metric being edited. The API has no id for one — position is its identity — but position is also what
 * dragging changes, so keying rows by index makes a focused input keep its DOM node while the metric under
 * it changes, and the next keystroke edits the wrong row. `key` travels with the object instead.
 */
export type DraftMetric = StockMovementMetricDto & {key: string};

/**
 * Keys for a preset straight off the server. Derived from the index rather than generated, so they stay
 * the same across renders while nothing has been edited; the first edit carries them into the draft, and
 * only genuinely new rows need a fresh one.
 */
export function withKeys(metrics: StockMovementMetricDto[]): DraftMetric[] {
  return metrics.map((metric, index) => ({...metric, key: `saved-${index}`}));
}

export function newMetricKey(): string {
  return `new-${crypto.randomUUID()}`;
}

/** The `key` is ours, not the API's — it never goes out in a request body. */
export function stripKeys(metrics: DraftMetric[]): StockMovementMetricDto[] {
  return metrics.map(({key: _key, ...metric}) => metric);
}
