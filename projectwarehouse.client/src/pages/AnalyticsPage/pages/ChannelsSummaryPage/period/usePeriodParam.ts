import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {
  type Period,
  PERIOD_PRESETS,
  type PeriodSelection,
  parsePeriod,
  resolvePeriod,
  serializePeriod,
} from "./periodSelection";

export const CARD_PERIOD_PRESETS = ["page", ...PERIOD_PRESETS] as const;

/** A period kept in `key`; the default leaves no param, so an untouched URL stays clean. */
export function usePeriodParam(key: string, fallback: PeriodSelection) {
  const fallbackQuery = serializePeriod(fallback);
  return useSyncedWithQueryState(
    key,
    (q) => parsePeriod(q, fallback),
    (v) => (serializePeriod(v) === fallbackQuery ? null : serializePeriod(v)),
  );
}

/** A card's own period, kept in `key`; with no param the card follows the page filter. */
export function useCardPeriod(key: string, pagePeriod: Period) {
  const [selection, setSelection] = usePeriodParam(key, {preset: "page"});
  return {selection, setSelection, period: resolvePeriod(selection, pagePeriod)};
}
