import {
  channelSelectionQuery,
  parseList,
  parseStep,
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
  // Display state of the chart, kept out of `query` so switching it does not refetch the page
  const [step, setStep] = useSyncedWithQueryState("step", parseStep, (v) => v);
  // Until picked, the withholdings card follows the accruals step and shares its response
  const [withholdingsStep, setWithholdingsStep] = useSyncedWithQueryState(
    "whstep",
    parseStep,
    (v) => v,
  );
  const [showTotal, setShowTotal] = useSyncedWithQueryState(
    "total",
    (q) => q !== "0",
    (v) => (v ? null : "0"),
  );

  const today = todayDateOnly();
  const {from, to} = resolvePeriod(selection, {from: today, to: today});
  const period = selection.preset === "allTime" ? {} : {From: from, To: to};

  return {
    query: {...period, ...channelSelectionQuery(channels)},
    selection,
    from,
    to,
    channels,
    step,
    withholdingsStep: withholdingsStep ?? step,
    showTotal,
    setSelection: (value: PeriodSelection) => {
      // A new preset changes the period's scale, so the steps go back to the server's pick
      if (value.preset !== selection.preset) {
        setStep(null);
        setWithholdingsStep(null);
      }
      setSelection(value);
    },
    setChannels,
    setStep,
    setWithholdingsStep,
    setShowTotal,
  };
}
