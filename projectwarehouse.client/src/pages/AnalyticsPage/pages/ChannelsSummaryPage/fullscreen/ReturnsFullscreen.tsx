import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsReturnsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMoneyMode} from "@/api/types.gen";
import {channelSelectionQuery} from "@/components/analytics/channelsQuery";
import StepMeasureToggles from "../StepMeasureToggles";
import ChartFullscreenDialog from "./ChartFullscreenDialog";
import type {useChartFullscreen} from "./useChartFullscreen";

interface ReturnsFullscreenProps {
  state: ReturnType<typeof useChartFullscreen>;
  moneyMode: AnalyticsMoneyMode;
}

function ReturnsFullscreen({state, moneyMode}: ReturnsFullscreenProps) {
  const open = state.view === "returns";
  const {data, error, isFetching} = useQuery({
    ...analyticsGetChannelsReturnsOptions({
      query: {
        From: state.period.from,
        To: state.period.to,
        MoneyMode: moneyMode,
        ...channelSelectionQuery(state.channels),
        Step: state.step ?? undefined,
      },
    }),
    enabled: open,
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  return (
    <ChartFullscreenDialog
      open={open}
      state={state}
      title="Возвраты по дате возврата, шт."
      toggles={<StepMeasureToggles step={data?.step ?? state.step} onStepChange={state.setStep} />}
      withDirect={false}
      data={data}
      isFetching={isFetching}
      error={error}
    />
  );
}

export default ReturnsFullscreen;
