import {Alert, Box, Chip, IconButton, Stack, Tooltip, Typography, useTheme} from "@mui/material";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsReturnsOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsStep, MarketplaceReturnKind} from "@/api/types.gen";
import {
  MARKETPLACE_RETURN_COMPENSATION_LABELS,
  MARKETPLACE_RETURN_KIND_LABELS,
} from "@/components/orders/marketplace/marketplaceOrderUtils";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatMoney, formatNumber, formatPercent} from "../channelsSummaryUtils";
import ChannelsLineChart from "../charts/ChannelsLineChart";
import StackedAccountsBar, {type StackPart} from "../charts/StackedAccountsBar";
import CountsTable from "../CountsTable";
import PeriodPicker from "../period/PeriodPicker";
import type {PeriodSelection} from "../period/periodSelection";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "../period/usePeriodParam";
import SummaryCard from "../SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

interface ReturnsCardProps {
  filters: ReturnType<typeof useChannelsSummaryFilters>;
  /** Called with the period and step the card shows, for the fullscreen chart to open with. */
  onExpand: (selection: PeriodSelection, step: AnalyticsStep | null) => void;
}

function ReturnsCard({filters, onExpand}: ReturnsCardProps) {
  const theme = useTheme();
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("returnperiod", pagePeriod);
  const followsPage = selection.preset === "page";
  // The page step is picked for the page period; another period gets the server's own pick
  const step = followsPage ? filters.step : null;

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsReturnsOptions({
      query: {...filters.query, From: period.from, To: period.to, Step: step ?? undefined},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const parts: StackPart<MarketplaceReturnKind>[] = [
    {
      key: "customerReturn",
      label: MARKETPLACE_RETURN_KIND_LABELS.customerReturn,
      color: theme.palette.warning.main,
    },
    {
      key: "partialRefusal",
      label: MARKETPLACE_RETURN_KIND_LABELS.partialRefusal,
      color: theme.palette.secondary.main,
    },
  ];

  const sold = data?.accounts.reduce((sum, a) => sum + a.soldUnits, 0) ?? 0;
  const returned = data?.accounts.reduce((sum, a) => sum + a.returnedUnits, 0) ?? 0;
  const moneyByCurrency = new Map<string, number>();
  for (const money of data?.accounts.flatMap((a) => a.money) ?? [])
    moneyByCurrency.set(
      money.currencyCode,
      (moneyByCurrency.get(money.currencyCode) ?? 0) + money.amount,
    );

  return (
    <SummaryCard
      title="Возвраты"
      isFetching={isFetching}
      actions={
        <>
          <PeriodPicker
            variant="select"
            presets={CARD_PERIOD_PRESETS}
            value={selection}
            onChange={setSelection}
            pagePeriod={pagePeriod}
          />
          <Tooltip title="Развернуть на всю вкладку">
            <IconButton onClick={() => onExpand(followsPage ? filters.selection : selection, step)}>
              <OpenInFullIcon />
            </IconButton>
          </Tooltip>
        </>
      }
      subtitle={
        data &&
        data.accounts.length > 0 && (
          <>
            {formatNumber(returned)} шт. · {formatPercent(sold > 0 ? returned / sold : null)}
            {data.returnsImmature && (
              <Tooltip
                title={`Возвраты ещё поступают: период закончился меньше ${data.returnsMaturityDays} дн. назад`}
              >
                <Box component="span" sx={{color: "warning.main", cursor: "help"}}>
                  *
                </Box>
              </Tooltip>
            )}
          </>
        )
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {data &&
        (data.accounts.length === 0 ? (
          <Typography color="text.secondary">
            Возвраты есть только у магазинов маркетплейсов
          </Typography>
        ) : (
          <Stack spacing={2}>
            {returned > 0 ? (
              <StackedAccountsBar
                parts={parts}
                rows={data.accounts.map((a) => ({
                  id: a.marketplaceAccountId,
                  label: a.name,
                  values: Object.fromEntries(a.byKind.map((k) => [k.kind, k.units])),
                }))}
              />
            ) : (
              <Typography color="text.secondary">Возвратов по продажам периода нет</Typography>
            )}

            {moneyByCurrency.size > 0 && (
              <Typography variant="body2">
                На сумму{" "}
                {[...moneyByCurrency]
                  .map(([currency, amount]) => formatMoney(amount, currency))
                  .join(", ")}
              </Typography>
            )}

            <Box>
              <Typography variant="body2" color="text.secondary" sx={{mb: 0.5}}>
                Объём по дате возврата
              </Typography>
              <ChannelsLineChart
                intervals={data.intervals}
                series={data.series}
                step={data.step}
                height={200}
              />
            </Box>

            <CountsTable
              label="Причина"
              valueLabel="Штук"
              rows={data.topReasons.map((r) => ({key: r.reason, count: r.units}))}
            />

            {data.compensations.length > 0 && (
              <Stack
                direction="row"
                useFlexGap
                sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}
              >
                <Typography variant="body2" color="text.secondary">
                  Компенсации:
                </Typography>
                {data.compensations.map((c) => (
                  <Chip
                    key={c.status}
                    size="small"
                    variant="outlined"
                    label={`${MARKETPLACE_RETURN_COMPENSATION_LABELS[c.status]}: ${formatNumber(c.count)}`}
                  />
                ))}
              </Stack>
            )}
          </Stack>
        ))}
    </SummaryCard>
  );
}

export default ReturnsCard;
