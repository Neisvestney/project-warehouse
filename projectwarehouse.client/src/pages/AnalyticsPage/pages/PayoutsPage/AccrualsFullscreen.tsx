import {ToggleButton, ToggleButtonGroup} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetPayoutsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import {formatCompact} from "@/components/analytics/analyticsFormat";
import ChartFullscreenDialog from "@/components/analytics/charts/ChartFullscreenDialog";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import TotalToggle from "@/components/analytics/charts/TotalToggle";
import {ACCRUALS_TITLE, moneyFormat, toChartData} from "./accrualsChart";
import type {useAccrualsFullscreen} from "./useAccrualsFullscreen";
import {PAYOUTS_PERIOD_PRESETS} from "./usePayoutsFilters";

interface AccrualsFullscreenProps {
  state: ReturnType<typeof useAccrualsFullscreen>;
}

function AccrualsFullscreen({state}: AccrualsFullscreenProps) {
  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetPayoutsTimeseriesOptions({
      query: {...state.query, Step: state.step ?? undefined},
    }),
    enabled: state.open,
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const currencies = data?.currencies ?? [];
  const block = currencies.find((c) => c.currencyCode === state.currency) ?? currencies[0];

  return (
    <ChartFullscreenDialog
      open={state.open}
      state={state}
      title={ACCRUALS_TITLE}
      presets={PAYOUTS_PERIOD_PRESETS}
      reportedPeriod={
        state.selection.preset === "allTime" && data ? {from: data.from, to: data.to} : undefined
      }
      toggles={
        <>
          <StepMeasureToggles step={data?.step ?? state.step} onStepChange={state.setStep} />
          <TotalToggle value={state.showTotal} onChange={state.setShowTotal} />
          {currencies.length > 1 && block && (
            <ToggleButtonGroup
              exclusive
              size="small"
              value={block.currencyCode}
              onChange={(_, value: string | null) => value && state.setCurrency(value)}
            >
              {currencies.map((c) => (
                <ToggleButton key={c.currencyCode} value={c.currencyCode}>
                  {c.currencyCode}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          )}
        </>
      }
      withDirect={false}
      withTotal={state.showTotal}
      format={moneyFormat(block?.currencyCode ?? "")}
      axisFormat={formatCompact}
      data={data && block && !isError ? toChartData(data, block) : undefined}
      isFetching={isFetching}
      error={error}
    />
  );
}

export default AccrualsFullscreen;
