import {Alert, Box, Stack, Typography} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetPayoutsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import type {PayoutsWithholdingsDto} from "@/api/types.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import SummaryCard from "@/components/analytics/SummaryCard";
import {extractErrorMessage} from "@/utils/errorUtils";
import type {usePayoutsFilters} from "./usePayoutsFilters";
import WithholdingsChart from "./WithholdingsChart";
import {withheldShare} from "./withholdings";

function sum(values: (number | null)[]) {
  return values.reduce<number>((total, v) => total + (v ?? 0), 0);
}

function summary({sales, withheldByPosting, withheldByShop}: PayoutsWithholdingsDto) {
  const total = sum(sales);
  const byPosting = sum(withheldByPosting);
  const byShop = sum(withheldByShop);
  return [
    `удержано ${formatPercent(withheldShare(byPosting + byShop, total))}`,
    `по отправлениям ${formatPercent(withheldShare(byPosting, total))}`,
    `по магазину ${formatPercent(withheldShare(byShop, total))}`,
  ].join(" · ");
}

function WithholdingsCard({filters}: {filters: ReturnType<typeof usePayoutsFilters>}) {
  // With the same step as the accruals dynamics card both cards read one cache entry
  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetPayoutsTimeseriesOptions({
      query: {...filters.query, Step: filters.withholdingsStep ?? undefined},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const single = data?.currencies.length === 1 ? data.currencies[0] : null;

  return (
    <SummaryCard
      title="Продажи и удержания"
      hint="Каждая строка журнала — в интервале дня её начисления, поэтому удержания за продажу могут попасть в соседний интервал, а строки магазина приходят разом."
      subtitle={single && summary(single.withholdings)}
      isFetching={isFetching}
      actions={
        <StepMeasureToggles
          step={data?.step ?? filters.withholdingsStep}
          onStepChange={filters.setWithholdingsStep}
        />
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {data &&
        !isError &&
        (data.currencies.length === 0 ? (
          <Box sx={{height: 280, display: "flex", alignItems: "center", justifyContent: "center"}}>
            <Typography color="text.secondary">Нет начислений за период</Typography>
          </Box>
        ) : (
          <Stack spacing={2}>
            {data.currencies.map((block) => (
              <Box key={block.currencyCode}>
                {!single && (
                  <Typography variant="subtitle2" sx={{px: 1}}>
                    {block.currencyCode} · {summary(block.withholdings)}
                  </Typography>
                )}
                <WithholdingsChart
                  intervals={data.intervals}
                  step={data.step}
                  withholdings={block.withholdings}
                  currencyCode={block.currencyCode}
                  height={280}
                />
              </Box>
            ))}
          </Stack>
        ))}
    </SummaryCard>
  );
}

export default WithholdingsCard;
