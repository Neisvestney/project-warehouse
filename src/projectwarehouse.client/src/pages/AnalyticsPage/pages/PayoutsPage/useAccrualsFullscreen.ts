import type {AnalyticsStep} from "@/api/types.gen";
import {
  channelSelectionQuery,
  parseList,
  parseStep,
  serializeList,
} from "@/components/analytics/channelsQuery";
import {
  type PeriodSelection,
  resolvePeriod,
  serializePeriod,
  withPreset,
} from "@/components/analytics/period/periodSelection";
import {usePeriodParam} from "@/components/analytics/period/usePeriodParam";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {todayDateOnly} from "@/utils/dateOnly";

export interface AccrualsFullscreenStart {
  channels: string[];
  selection: PeriodSelection;
  step: AnalyticsStep | null;
  total: boolean;
}

const FULLSCREEN_PARAMS = ["full", "fsperiod", "fsstep", "fstotal", "fsch", "fscur"];

/** The expanded accruals chart, in the URL beside the page's filters and independent of them once open. */
export function useAccrualsFullscreen() {
  const {setParam} = useSearchParamsContext();
  const today = todayDateOnly();
  const currentYear = withPreset("year", {from: today, to: today});

  const [open] = useSyncedWithQueryState(
    "full",
    (q) => q === "accruals",
    (v) => (v ? "accruals" : null),
  );
  const [selection, setSelection] = usePeriodParam("fsperiod", currentYear);
  const [step, setStep] = useSyncedWithQueryState("fsstep", parseStep, (v) => v);
  const [showTotal, setShowTotal] = useSyncedWithQueryState(
    "fstotal",
    (q) => q !== "0",
    (v) => (v ? null : "0"),
  );
  const [channels, setChannels] = useSyncedWithQueryState("fsch", parseList, serializeList);
  const [currency, setCurrency] = useSyncedWithQueryState(
    "fscur",
    (q) => q,
    (v) => v,
  );

  const period = resolvePeriod(selection, {from: today, to: today});
  const dates = selection.preset === "allTime" ? {} : {From: period.from, To: period.to};

  return {
    open,
    selection,
    period,
    query: {...dates, ...channelSelectionQuery(channels)},
    step,
    showTotal,
    channels,
    currency,
    // Every fullscreen param goes in one batched navigation, so opening and closing cost one history entry
    openWith: (start: AccrualsFullscreenStart) => {
      setParam("full", "accruals");
      setParam("fsch", serializeList(start.channels));
      setParam("fsperiod", serializePeriod(start.selection));
      setParam("fsstep", start.step);
      setParam("fstotal", start.total ? null : "0");
    },
    close: () => {
      for (const key of FULLSCREEN_PARAMS) setParam(key, null);
    },
    setSelection: (value: PeriodSelection) => {
      if (value.preset !== selection.preset) setStep(null);
      setSelection(value);
    },
    setStep,
    setShowTotal,
    setChannels,
    setCurrency,
  };
}
