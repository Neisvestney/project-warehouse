import {Alert, IconButton, Tooltip} from "@mui/material";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMeasure, AnalyticsStep} from "@/api/types.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import ChannelsLineChart from "../charts/ChannelsLineChart";
import StepMeasureToggles from "../StepMeasureToggles";
import SummaryCard from "../SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

interface DynamicsCardProps {
  filters: ReturnType<typeof useChannelsSummaryFilters>;
  onExpand: () => void;
}

function DynamicsCard({filters, onExpand}: DynamicsCardProps) {
  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsTimeseriesOptions({
      query: {
        ...filters.query,
        Step: filters.step ?? undefined,
        Measure: filters.measure,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  return (
    <SummaryCard
      title={filters.measure === "units" ? "Динамика штук" : "Динамика заказов"}
      isFetching={isFetching}
      actions={
        <>
          <StepMeasureToggles
            step={data?.step ?? filters.step}
            measure={filters.measure}
            onStepChange={(value: AnalyticsStep) => filters.setStep(value)}
            onMeasureChange={(value: AnalyticsMeasure) => filters.setMeasure(value)}
          />
          <Tooltip title="Развернуть на всю вкладку">
            <IconButton onClick={onExpand}>
              <OpenInFullIcon />
            </IconButton>
          </Tooltip>
        </>
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {data && (
        <ChannelsLineChart
          intervals={data.intervals}
          series={data.series}
          step={data.step}
          height={280}
        />
      )}
    </SummaryCard>
  );
}

export default DynamicsCard;
