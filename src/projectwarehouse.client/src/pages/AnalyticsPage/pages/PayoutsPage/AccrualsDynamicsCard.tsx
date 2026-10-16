import {Alert, Box, IconButton, Stack, Tooltip, Typography} from "@mui/material";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetPayoutsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import {formatCompact} from "@/components/analytics/analyticsFormat";
import ChannelsLineChart from "@/components/analytics/charts/ChannelsLineChart";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import TotalToggle from "@/components/analytics/charts/TotalToggle";
import SummaryCard from "@/components/analytics/SummaryCard";
import {extractErrorMessage} from "@/utils/errorUtils";
import {ACCRUALS_TITLE, moneyFormat, toChartData} from "./accrualsChart";
import type {usePayoutsFilters} from "./usePayoutsFilters";

interface AccrualsDynamicsCardProps {
  filters: ReturnType<typeof usePayoutsFilters>;
  onExpand: () => void;
}

function AccrualsDynamicsCard({filters, onExpand}: AccrualsDynamicsCardProps) {
  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetPayoutsTimeseriesOptions({
      query: {...filters.query, Step: filters.step ?? undefined},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  return (
    <SummaryCard
      title={ACCRUALS_TITLE}
      hint="«Начислено» по интервалам: каждая строка журнала — в день её начисления, строки магазина тоже"
      isFetching={isFetching}
      actions={
        <>
          <StepMeasureToggles step={data?.step ?? filters.step} onStepChange={filters.setStep} />
          <TotalToggle value={filters.showTotal} onChange={filters.setShowTotal} />
          <Tooltip title="Развернуть на всю вкладку">
            <IconButton onClick={onExpand}>
              <OpenInFullIcon />
            </IconButton>
          </Tooltip>
        </>
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {/* A placeholder from the previous step would sit under toggles that already show the new one */}
      {data &&
        !isError &&
        (data.currencies.length === 0 ? (
          <Box sx={{height: 280, display: "flex", alignItems: "center", justifyContent: "center"}}>
            <Typography color="text.secondary">Нет начислений за период</Typography>
          </Box>
        ) : (
          <Stack spacing={2}>
            {data.currencies.map((block) => {
              const chart = toChartData(data, block);
              return (
                <Box key={block.currencyCode}>
                  {data.currencies.length > 1 && (
                    <Typography variant="subtitle2" sx={{px: 1}}>
                      {block.currencyCode}
                    </Typography>
                  )}
                  <ChannelsLineChart
                    intervals={chart.intervals}
                    series={chart.series}
                    step={chart.step}
                    height={280}
                    format={moneyFormat(block.currencyCode)}
                    axisFormat={formatCompact}
                    withTotal={filters.showTotal}
                  />
                </Box>
              );
            })}
          </Stack>
        ))}
    </SummaryCard>
  );
}

export default AccrualsDynamicsCard;
