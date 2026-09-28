import {Alert, Box, IconButton, Stack, Tooltip, Typography, useTheme} from "@mui/material";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsCancellationsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsStep, MarketplaceCancellationType} from "@/api/types.gen";
import {MARKETPLACE_CANCELLATION_TYPE_LABELS} from "@/components/orders/marketplace/marketplaceOrderUtils";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";
import ChannelsLineChart from "@/components/analytics/charts/ChannelsLineChart";
import {formatCount, formatShare, shareSeries} from "@/components/analytics/charts/chartSeries";
import StackedAccountsBar, {type StackPart, type StackRow} from "../charts/StackedAccountsBar";
import CountsTable from "../CountsTable";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import type {PeriodSelection} from "@/components/analytics/period/periodSelection";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import ShareToggle from "../ShareToggle";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import {useCardSummary} from "./useCardSummary";

const TYPE_ORDER: MarketplaceCancellationType[] = [
  "customer",
  "seller",
  "marketplace",
  "system",
  "delivery",
  "unknown",
];

interface CancellationsCardProps {
  filters: ReturnType<typeof useChannelsSummaryFilters>;
  /** Called with what the chart shows, for the fullscreen one to open with. */
  onExpand: (selection: PeriodSelection, step: AnalyticsStep | null, share: boolean) => void;
}

function CancellationsCard({filters, onExpand}: CancellationsCardProps) {
  const theme = useTheme();
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("cancelperiod", pagePeriod);
  const [share, setShare] = useSyncedWithQueryState(
    "cancelshare",
    (q) => q === "1",
    (v) => (v ? "1" : null),
  );
  const followsPage = selection.preset === "page";
  // The page step is picked for the page period; another period gets the server's own pick
  const step = followsPage ? filters.step : null;

  const {data: summary, error, isError, isFetching} = useCardSummary(filters, period);
  const chart = useQuery({
    ...analyticsGetChannelsCancellationsOptions({
      query: {...filters.channelQuery, From: period.from, To: period.to, Step: step ?? undefined},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const shops = summary?.rows.filter((r) => r.kind === "marketplace") ?? [];

  const typeColors: Record<MarketplaceCancellationType, string> = {
    customer: theme.palette.primary.main,
    seller: theme.palette.warning.main,
    marketplace: theme.palette.secondary.main,
    system: theme.palette.info.main,
    delivery: theme.palette.success.main,
    unknown: theme.palette.grey[500],
  };

  const parts: StackPart<MarketplaceCancellationType>[] = TYPE_ORDER.map((type) => ({
    key: type,
    label: MARKETPLACE_CANCELLATION_TYPE_LABELS[type],
    color: typeColors[type],
  }));

  const rows: StackRow<MarketplaceCancellationType>[] = shops.map((shop) => {
    const entry = summary?.cancellations.find(
      (c) => c.marketplaceAccountId === shop.marketplaceAccountId,
    );
    return {
      id: shop.marketplaceAccountId!,
      label: shop.name ?? "",
      values: Object.fromEntries((entry?.byType ?? []).map((t) => [t.type, t.count])),
    };
  });

  const total = shops.reduce((sum, s) => sum + s.cancellations, 0);
  const sales = shops.reduce((sum, s) => sum + s.orders, 0);
  const afterShip = summary?.cancellations.reduce((sum, c) => sum + c.afterShip, 0) ?? 0;

  return (
    <SummaryCard
      title="Отмены по инициатору"
      isFetching={isFetching || chart.isFetching}
      subtitle={
        shops.length > 0 &&
        `${formatNumber(total)} · ${formatPercent(total + sales > 0 ? total / (total + sales) : null)}`
      }
      actions={
        <>
          <PeriodPicker
            variant="compact"
            presets={CARD_PERIOD_PRESETS}
            value={selection}
            onChange={setSelection}
            pagePeriod={pagePeriod}
          />
          <Tooltip title="Развернуть на всю вкладку">
            <IconButton
              onClick={() => onExpand(followsPage ? filters.selection : selection, step, share)}
            >
              <OpenInFullIcon />
            </IconButton>
          </Tooltip>
        </>
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {chart.isError && <Alert severity="error">{extractErrorMessage(chart.error)}</Alert>}
      {summary &&
        (shops.length === 0 ? (
          <Typography color="text.secondary">
            Разбивка есть только у магазинов маркетплейсов
          </Typography>
        ) : (
          <Stack spacing={1.5}>
            {total > 0 ? (
              <StackedAccountsBar rows={rows} parts={parts} />
            ) : (
              <Typography color="text.secondary">Отмен за период нет</Typography>
            )}
            <Typography variant="body2" color="text.secondary">
              После отгрузки: {formatNumber(afterShip)}
            </Typography>
            {chart.data && (
              <Box>
                <Stack direction="row" sx={{alignItems: "center", mb: 0.5}}>
                  <Typography variant="body2" color="text.secondary">
                    {share ? "Доля отмен" : "Отмены"} по дате заказа
                  </Typography>
                  <Box sx={{ml: "auto"}}>
                    <ShareToggle share={share} onChange={setShare} countLabel="Заказы" />
                  </Box>
                </Stack>
                <ChannelsLineChart
                  intervals={chart.data.intervals}
                  series={
                    share ? shareSeries(chart.data.series, chart.data.orders) : chart.data.series
                  }
                  step={chart.data.step}
                  height={200}
                  format={share ? formatShare : formatCount}
                />
              </Box>
            )}
            <CountsTable
              label="Причина"
              valueLabel="Отмен"
              rows={summary.topCancelReasons.map((r) => ({key: r.reason, count: r.count}))}
            />
          </Stack>
        ))}
    </SummaryCard>
  );
}

export default CancellationsCard;
