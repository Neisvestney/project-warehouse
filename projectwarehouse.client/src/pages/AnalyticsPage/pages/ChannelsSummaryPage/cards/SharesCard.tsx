import {
  Alert,
  Box,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsTimeseriesOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMeasure} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import SharesAreaChart from "../charts/SharesAreaChart";
import {useChannelColor} from "../charts/useChannelColor";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import {useCardSummary} from "./useCardSummary";

interface Segment {
  key: string;
  label: string;
  share: number;
  color: string;
}

function ShareBar({label, segments}: {label: string; segments: Segment[]}) {
  return (
    <Box sx={{display: "grid", gridTemplateColumns: "80px 1fr", gap: 1.5, alignItems: "center"}}>
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Box
        sx={{
          display: "flex",
          height: 22,
          borderRadius: 0.75,
          overflow: "hidden",
          bgcolor: "action.hover",
        }}
      >
        {segments
          .filter((s) => s.share > 0)
          .map((s) => (
            <Tooltip key={s.key} title={`${s.label}: ${formatPercent(s.share)}`}>
              <Box sx={{width: `${s.share * 100}%`, bgcolor: s.color}} />
            </Tooltip>
          ))}
      </Box>
    </Box>
  );
}

function SharesCard({filters}: {filters: ReturnType<typeof useChannelsSummaryFilters>}) {
  const channelColor = useChannelColor();
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("shareperiod", pagePeriod);
  const [measure, setMeasure] = useSyncedWithQueryState<AnalyticsMeasure>(
    "sharemeasure",
    (q) => (q === "orders" ? "orders" : "units"),
    (v) => (v === "orders" ? v : null),
  );
  // The page step is picked for the page period; another period gets the server's own pick
  const step = selection.preset === "page" ? filters.step : null;

  const {data: summary, error, isError, isFetching} = useCardSummary(filters, period);
  const chart = useQuery({
    ...analyticsGetChannelsTimeseriesOptions({
      query: {
        ...filters.channelQuery,
        From: period.from,
        To: period.to,
        Step: step ?? undefined,
        Measure: measure,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const channels =
    summary?.rows.filter((r) => r.kind === "marketplace" || r.kind === "direct") ?? [];

  const unitSegments: Segment[] = channels.map((r) => ({
    key: r.marketplaceAccountId ?? r.kind,
    label: r.kind === "direct" ? "Прямые" : (r.name ?? ""),
    share: r.unitsShare ?? 0,
    color: channelColor(r.marketplaceAccountId),
  }));

  const currencies = [...new Set(channels.flatMap((r) => r.money.map((m) => m.currencyCode)))];

  return (
    <SummaryCard
      title="Доля каналов"
      isFetching={isFetching || chart.isFetching}
      actions={
        <PeriodPicker
          variant="compact"
          presets={CARD_PERIOD_PRESETS}
          value={selection}
          onChange={setSelection}
          pagePeriod={pagePeriod}
        />
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {chart.isError && <Alert severity="error">{extractErrorMessage(chart.error)}</Alert>}
      {!summary ? null : channels.length === 0 ? (
        <Typography color="text.secondary">Нет выбранных каналов</Typography>
      ) : (
        <Stack spacing={2}>
          <ShareBar label="Штуки" segments={unitSegments} />
          {currencies.map((currency) => (
            <ShareBar
              key={currency}
              label={currencies.length > 1 ? `Выручка, ${currency}` : "Выручка"}
              segments={channels.map((r) => ({
                key: r.marketplaceAccountId ?? r.kind,
                label: r.name ?? "",
                share: r.money.find((m) => m.currencyCode === currency)?.revenueShare ?? 0,
                color: channelColor(r.marketplaceAccountId),
              }))}
            />
          ))}
          <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 2}}>
            {unitSegments.map((s) => (
              <Stack key={s.key} direction="row" spacing={0.75} sx={{alignItems: "center"}}>
                <Box sx={{width: 10, height: 10, borderRadius: 0.5, bgcolor: s.color}} />
                <Typography variant="caption">{s.label}</Typography>
              </Stack>
            ))}
          </Stack>
          {channels.some((r) => r.kind === "direct") && (
            <Typography variant="caption" color="text.secondary">
              У прямых заказов нет денег, поэтому в полосе выручки их нет
            </Typography>
          )}
          {chart.data && (
            <Box>
              <Stack direction="row" sx={{alignItems: "center", mb: 0.5}}>
                <Typography variant="body2" color="text.secondary">
                  Как менялась доля
                </Typography>
                <ToggleButtonGroup
                  exclusive
                  size="small"
                  value={measure}
                  onChange={(_, value: AnalyticsMeasure | null) => value && setMeasure(value)}
                  sx={{ml: "auto"}}
                >
                  <ToggleButton value="units">Штуки</ToggleButton>
                  <ToggleButton value="orders">Заказы</ToggleButton>
                </ToggleButtonGroup>
              </Stack>
              <SharesAreaChart
                intervals={chart.data.intervals}
                series={chart.data.series}
                step={chart.data.step}
                height={240}
              />
            </Box>
          )}
        </Stack>
      )}
    </SummaryCard>
  );
}

export default SharesCard;
