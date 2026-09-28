import {Alert, Stack, Typography} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsLossesOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMeasure} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {extractErrorMessage} from "@/utils/errorUtils";
import {parseMeasure, parseStep} from "@/components/analytics/channelsQuery";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import LossesChart from "../charts/LossesChart";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import type {PeriodSelection} from "@/components/analytics/period/periodSelection";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import StepMeasureToggles from "@/components/analytics/charts/StepMeasureToggles";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import CardChannelSelect from "./CardChannelSelect";
import {useCardChannel} from "./useCardChannel";

function sum(values: (number | null | undefined)[]) {
  return values.reduce<number>((total, v) => total + (v ?? 0), 0);
}

function LossesCard({filters}: {filters: ReturnType<typeof useChannelsSummaryFilters>}) {
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("lossperiod", pagePeriod);
  const channel = useCardChannel(filters, "lossch");
  const [storedStep, setStep] = useSyncedWithQueryState("lossstep", parseStep, (v) => v);
  const [measure, setMeasure] = useSyncedWithQueryState("lossmeasure", parseMeasure, (v) =>
    v === "units" ? v : null,
  );
  // Until a step is picked here, the page step fits the page period; another period gets the server's pick
  const step = storedStep ?? (selection.preset === "page" ? filters.step : null);

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsLossesOptions({
      query: {
        ...filters.channelQuery,
        ...channel.query,
        From: period.from,
        To: period.to,
        Step: step ?? undefined,
        Measure: measure,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const points = data?.points ?? [];
  const cancellations = sum(points.map((p) => p.cancellations));
  const counted = sum(points.map((p) => p.saleOrders)) + cancellations;
  const shopUnits = sum(points.map((p) => p.shopUnits));
  const returned = sum(points.map((p) => p.returnedUnits));
  const hasShops = points.some((p) => p.shopUnits != null);

  return (
    <SummaryCard
      title="Продажи и потери"
      isFetching={isFetching}
      subtitle={
        data &&
        [
          `отмены ${formatPercent(counted > 0 ? cancellations / counted : null)}`,
          hasShops && `возвраты ${formatPercent(shopUnits > 0 ? returned / shopUnits : null)}`,
        ]
          .filter(Boolean)
          .join(" · ")
      }
      actions={
        <>
          <StepMeasureToggles
            step={data?.step ?? step}
            onStepChange={setStep}
            measure={measure}
            onMeasureChange={(value: AnalyticsMeasure) => setMeasure(value)}
          />
          <CardChannelSelect
            options={channel.options}
            value={channel.channel}
            onChange={channel.setChannel}
          />
          <PeriodPicker
            variant="compact"
            presets={CARD_PERIOD_PRESETS}
            value={selection}
            onChange={(value: PeriodSelection) => {
              // A new preset changes the period's scale, so the step goes back to the default pick
              if (value.preset !== selection.preset) setStep(null);
              setSelection(value);
            }}
            pagePeriod={pagePeriod}
          />
        </>
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {data && (
        <Stack spacing={1}>
          <LossesChart data={data} height={300} />
          <Typography variant="caption" color="text.secondary">
            Всё привязано к дате заказа: возврат попадает в интервал, когда товар продали, а не
            когда его вернули.
            {hasShops
              ? " Возвраты есть только у магазинов, поэтому их доля считается от штук магазинов."
              : " У прямых заказов возвратов нет."}
          </Typography>
        </Stack>
      )}
    </SummaryCard>
  );
}

export default LossesCard;
