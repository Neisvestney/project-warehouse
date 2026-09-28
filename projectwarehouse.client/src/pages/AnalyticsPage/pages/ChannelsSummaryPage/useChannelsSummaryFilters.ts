import type {AnalyticsMoneyMode} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {todayDateOnly} from "@/utils/dateOnly";
import {
  channelSelectionQuery,
  parseList,
  parseMeasure,
  parseStep,
  serializeList,
} from "@/components/analytics/channelsQuery";
import {type PeriodSelection, resolvePeriod} from "@/components/analytics/period/periodSelection";
import {usePeriodParam} from "@/components/analytics/period/usePeriodParam";

export const DEFAULT_PAGE_PERIOD: PeriodSelection = {preset: "lastMonth"};

export function useChannelsSummaryFilters() {
  const [selection, setSelection] = usePeriodParam("period", DEFAULT_PAGE_PERIOD);
  // Empty means every channel — a selection that removes all of them would show nothing at all
  const [channels, setChannels] = useSyncedWithQueryState("channels", parseList, serializeList);
  const [directTagIds, setDirectTagIds] = useSyncedWithQueryState("tags", parseList, serializeList);
  const [moneyMode, setMoneyMode] = useSyncedWithQueryState<AnalyticsMoneyMode>(
    "money",
    (q) => (q === "payout" ? "payout" : "price"),
    (v) => (v === "payout" ? v : null),
  );

  // Display state of the charts, kept out of `query` so switching it does not refetch the table
  const [step, setStep] = useSyncedWithQueryState("step", parseStep, (v) => v);
  const [measure, setMeasure] = useSyncedWithQueryState("measure", parseMeasure, (v) =>
    v === "units" ? v : null,
  );
  const [showTotal, setShowTotal] = useSyncedWithQueryState(
    "total",
    (q) => q !== "0",
    (v) => (v ? null : "0"),
  );

  const today = todayDateOnly();
  const {from, to} = resolvePeriod(selection, {from: today, to: today});

  // The channel part alone, for the reports that count no money
  const channelQuery = {...channelSelectionQuery(channels), DirectTagIds: directTagIds};
  const query = {From: from, To: to, MoneyMode: moneyMode, ...channelQuery};

  return {
    query,
    channelQuery,
    selection,
    from,
    to,
    channels,
    directTagIds,
    moneyMode,
    step,
    measure,
    showTotal,
    setSelection: (value: PeriodSelection) => {
      // A new preset changes the period's scale, so the step goes back to the server's pick
      if (value.preset !== selection.preset) setStep(null);
      setSelection(value);
    },
    setChannels,
    setDirectTagIds,
    setMoneyMode,
    setStep,
    setMeasure,
    setShowTotal,
  };
}
