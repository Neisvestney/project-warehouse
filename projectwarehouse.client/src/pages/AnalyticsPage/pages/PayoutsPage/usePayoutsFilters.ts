import {
  channelSelectionQuery,
  parseList,
  serializeList,
} from "@/components/analytics/channelsQuery";
import {type PeriodSelection, resolvePeriod} from "@/components/analytics/period/periodSelection";
import {usePeriodParam} from "@/components/analytics/period/usePeriodParam";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {todayDateOnly} from "@/utils/dateOnly";

const DEFAULT_PERIOD: PeriodSelection = {preset: "lastMonth"};

export const PAYOUTS_PERIOD_PRESETS = [
  "month",
  "quarter",
  "year",
  "lastMonth",
  "lastYear",
  "allTime",
  "custom",
] as const;

/** «За всё время» sends no dates; `from`/`to` are then today, and the page shows the span the server reports. */
export function usePayoutsFilters() {
  const [selection, setSelection] = usePeriodParam("period", DEFAULT_PERIOD);
  const [channels, setChannels] = useSyncedWithQueryState("channels", parseList, serializeList);

  const today = todayDateOnly();
  const {from, to} = resolvePeriod(selection, {from: today, to: today});
  const period = selection.preset === "allTime" ? {} : {From: from, To: to};

  return {
    query: {...period, ...channelSelectionQuery(channels)},
    selection,
    from,
    to,
    channels,
    setSelection,
    setChannels,
  };
}
