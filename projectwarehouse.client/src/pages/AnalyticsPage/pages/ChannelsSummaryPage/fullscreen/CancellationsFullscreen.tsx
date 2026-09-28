import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsCancellationsOptions} from "@/api/@tanstack/react-query.gen";
import {channelSelectionQuery} from "@/components/analytics/channelsQuery";
import {formatCount, formatShare, shareSeries} from "@/components/analytics/charts/chartSeries";
import ShareToggle from "../ShareToggle";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import ChartFullscreenDialog from "@/components/analytics/charts/ChartFullscreenDialog";
import type {useChartFullscreen} from "./useChartFullscreen";

function CancellationsFullscreen({state}: {state: ReturnType<typeof useChartFullscreen>}) {
  const open = state.view === "cancellations";
  const {data, error, isFetching} = useQuery({
    ...analyticsGetChannelsCancellationsOptions({
      query: {
        From: state.period.from,
        To: state.period.to,
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
      title={state.share ? "Доля отмен по дате заказа" : "Отмены по дате заказа"}
      toggles={
        <>
          <StepMeasureToggles step={data?.step ?? state.step} onStepChange={state.setStep} />
          <ShareToggle share={state.share} onChange={state.setShare} countLabel="Заказы" />
        </>
      }
      withDirect={false}
      format={state.share ? formatShare : formatCount}
      data={
        data && {
          ...data,
          series: state.share ? shareSeries(data.series, data.orders) : data.series,
        }
      }
      isFetching={isFetching}
      error={error}
    />
  );
}

export default CancellationsFullscreen;
