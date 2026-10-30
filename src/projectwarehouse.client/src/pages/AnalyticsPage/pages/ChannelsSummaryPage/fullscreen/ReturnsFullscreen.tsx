import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsReturnsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMoneyMode} from "@/api/types.gen";
import {channelSelectionQuery} from "@/components/analytics/channelsQuery";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import ChartFullscreenDialog from "@/components/analytics/charts/ChartFullscreenDialog";
import type {useChartFullscreen} from "./useChartFullscreen";
import ShareToggle from "../ShareToggle";
import {formatCount, formatShare, shareSeries} from "@/components/analytics/charts/chartSeries";

interface ReturnsFullscreenProps {
  state: ReturnType<typeof useChartFullscreen>;
  moneyMode: AnalyticsMoneyMode;
  catalogItemIds: string[];
}

function ReturnsFullscreen({state, moneyMode, catalogItemIds}: ReturnsFullscreenProps) {
  const open = state.view === "returns";
  const {data, error, isFetching} = useQuery({
    ...analyticsGetChannelsReturnsOptions({
      query: {
        From: state.period.from,
        To: state.period.to,
        MoneyMode: moneyMode,
        ...channelSelectionQuery(state.channels),
        CatalogItemIds: catalogItemIds,
        Step: state.step ?? undefined,
        DeliveredOnly: state.share,
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
      title={
        state.share
          ? "Доля возвратов от выданных товаров по дате заказа"
          : "Возвраты по дате возврата, шт."
      }
      toggles={
        <>
          <StepMeasureToggles step={data?.step ?? state.step} onStepChange={state.setStep} />
          <ShareToggle share={state.share} onChange={state.setShare} countLabel="Штуки" />
        </>
      }
      withDirect={false}
      format={state.share ? formatShare : formatCount}
      data={
        data && {
          ...data,
          series: state.share ? shareSeries(data.series, data.soldSeries) : data.series,
        }
      }
      isFetching={isFetching}
      error={error}
    />
  );
}

export default ReturnsFullscreen;
