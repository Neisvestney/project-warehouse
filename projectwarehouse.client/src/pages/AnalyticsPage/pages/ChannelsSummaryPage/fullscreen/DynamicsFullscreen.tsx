import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import {channelSelectionQuery} from "@/components/analytics/channelsQuery";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import TotalToggle from "@/components/analytics/charts/TotalToggle";
import ChartFullscreenDialog from "@/components/analytics/charts/ChartFullscreenDialog";
import type {useChartFullscreen} from "./useChartFullscreen";

interface DynamicsFullscreenProps {
  state: ReturnType<typeof useChartFullscreen>;
  directTagIds: string[];
  catalogItemIds: string[];
}

function DynamicsFullscreen({state, directTagIds, catalogItemIds}: DynamicsFullscreenProps) {
  const open = state.view === "dynamics";
  const {data, error, isFetching} = useQuery({
    ...analyticsGetChannelsTimeseriesOptions({
      query: {
        From: state.period.from,
        To: state.period.to,
        ...channelSelectionQuery(state.channels),
        DirectTagIds: directTagIds,
        CatalogItemIds: catalogItemIds,
        Step: state.step ?? undefined,
        Measure: state.measure,
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
      title={state.measure === "units" ? "Штуки по периодам" : "Заказы по периодам"}
      toggles={
        <>
          <StepMeasureToggles
            step={data?.step ?? state.step}
            onStepChange={state.setStep}
            measure={state.measure}
            onMeasureChange={state.setMeasure}
          />
          <TotalToggle value={state.showTotal} onChange={state.setShowTotal} />
        </>
      }
      withDirect
      withTotal={state.showTotal}
      data={data}
      isFetching={isFetching}
      error={error}
    />
  );
}

export default DynamicsFullscreen;
