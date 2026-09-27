import type {AnalyticsMeasure, AnalyticsStep} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {useSearchParamsContext} from "@/contexts/SearchParams/SearchParamsContext";
import {todayDateOnly} from "@/utils/dateOnly";
import {
  parseList,
  parseMeasure,
  parseStep,
  serializeList,
} from "@/components/analytics/channelsQuery";
import {
  type Period,
  type PeriodSelection,
  resolvePeriod,
  serializePeriod,
  withPreset,
} from "@/components/analytics/period/periodSelection";
import {usePeriodParam} from "@/components/analytics/period/usePeriodParam";

export type FullscreenView = "dynamics" | "returns" | "cancellations";

const VIEWS: FullscreenView[] = ["dynamics", "returns", "cancellations"];

/** What the chart opens with: the period, step, measure and share toggle its card was showing. */
export interface FullscreenStart {
  channels: string[];
  selection: PeriodSelection;
  step: AnalyticsStep | null;
  measure?: AnalyticsMeasure;
  share?: boolean;
}

const FULLSCREEN_PARAMS = ["full", "fsperiod", "fsstep", "fsmeasure", "fsshare", "fsch"];

/**
 * State of a fullscreen chart, kept in the URL next to the page's own filters and independent of them:
 * it opens with what its card showed, and changing it there leaves the page as it was.
 * Only one chart is expanded at a time, so they all share the params and `full` tells which one is open.
 */
export function useChartFullscreen() {
  const {setParam} = useSearchParamsContext();
  const today = todayDateOnly();
  const currentYear = withPreset("year", {from: today, to: today});

  const [view] = useSyncedWithQueryState<FullscreenView | null>(
    "full",
    (q) => (VIEWS.includes(q as FullscreenView) ? (q as FullscreenView) : null),
    (v) => v,
  );
  const [selection, setSelection] = usePeriodParam("fsperiod", currentYear);
  const [step, setStep] = useSyncedWithQueryState("fsstep", parseStep, (v) => v);
  const [measure, setMeasure] = useSyncedWithQueryState("fsmeasure", parseMeasure, (v) =>
    v === "units" ? v : null,
  );
  const [share, setShare] = useSyncedWithQueryState(
    "fsshare",
    (q) => q === "1",
    (v) => (v ? "1" : null),
  );
  const [channels, setChannels] = useSyncedWithQueryState("fsch", parseList, serializeList);

  const period: Period = resolvePeriod(selection, {from: today, to: today});

  return {
    view,
    selection,
    period,
    step,
    measure,
    share,
    channels,
    // Every fullscreen param goes in one batched navigation, so opening and closing cost one history entry
    openWith: (value: FullscreenView, start: FullscreenStart) => {
      setParam("full", value);
      setParam("fsch", serializeList(start.channels));
      setParam("fsperiod", serializePeriod(start.selection));
      setParam("fsstep", start.step);
      setParam("fsmeasure", start.measure === "units" ? "units" : null);
      setParam("fsshare", start.share ? "1" : null);
    },
    close: () => {
      for (const key of FULLSCREEN_PARAMS) setParam(key, null);
    },
    setSelection: (value: PeriodSelection) => {
      // A new preset changes the period's scale, so the step goes back to the server's pick
      if (value.preset !== selection.preset) setStep(null);
      setSelection(value);
    },
    setStep,
    setMeasure,
    setShare,
    setChannels,
  };
}
