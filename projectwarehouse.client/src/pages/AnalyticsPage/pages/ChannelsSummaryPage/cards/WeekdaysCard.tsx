import type {ReactNode} from "react";
import {
  Alert,
  Box,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
  alpha,
  useTheme,
} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetChannelsWeekdaysOptions} from "@/api/@tanstack/react-query.gen";
import type {AnalyticsMeasure, WeekdayScale} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {formatDateOnly} from "@/utils/dateOnly";
import {extractErrorMessage} from "@/utils/errorUtils";
import {NOUNS, pluralCount} from "@/utils/pluralUtils";
import ChannelChip from "@/components/analytics/ChannelChip";
import {parseMeasure} from "@/components/analytics/channelsQuery";
import {formatAverage, formatPercent} from "@/components/analytics/analyticsFormat";
import {useChannelColor} from "@/components/analytics/charts/useChannelColor";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "@/components/analytics/period/usePeriodParam";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

interface HeatRowProps {
  label: ReactNode;
  values: (number | null)[];
  color: string;
  format: (value: number) => string;
  bold?: boolean;
  nested?: boolean;
}

/**
 * Intensity runs from the row's weakest day to its strongest: scaled from zero, a week of 88…99 would come
 * out one flat tone, and Direct next to a big shop would fade into a pale strip.
 */
function HeatRow({label, values, color, format, bold, nested}: HeatRowProps) {
  const theme = useTheme();
  const known = values.filter((v): v is number => v != null);
  const min = Math.min(...known);
  const max = Math.max(...known);

  function strength(value: number | null): number | null {
    if (value == null || max <= 0) return null;
    return max === min ? 0.5 : (value - min) / (max - min);
  }

  return (
    <TableRow>
      <TableCell
        sx={{whiteSpace: "nowrap", fontWeight: bold ? 600 : undefined, pl: nested ? 4 : undefined}}
      >
        {label}
      </TableCell>
      {values.map((value, i) => {
        const t = strength(value);
        return (
          <TableCell
            key={WEEKDAYS[i]}
            align="center"
            sx={{
              fontWeight: bold ? 600 : undefined,
              bgcolor: t == null ? undefined : alpha(color, 0.06 + 0.84 * t),
              // A near-solid cell needs the text color of its fill, not of the paper
              color: t != null && t > 0.6 ? theme.palette.getContrastText(color) : undefined,
            }}
          >
            {value == null ? "—" : format(value)}
          </TableCell>
        );
      })}
    </TableRow>
  );
}

function WeekdaysCard({filters}: {filters: ReturnType<typeof useChannelsSummaryFilters>}) {
  const theme = useTheme();
  const channelColor = useChannelColor();
  const pagePeriod = {from: filters.from, to: filters.to};
  const {selection, setSelection, period} = useCardPeriod("weekperiod", pagePeriod);
  const [measure, setMeasure] = useSyncedWithQueryState("weekmeasure", parseMeasure, (v) =>
    v === "units" ? v : null,
  );
  const [scale, setScale] = useSyncedWithQueryState<WeekdayScale>(
    "weekscale",
    (v) => (v === "weekShare" ? v : "average"),
    (v) => (v === "weekShare" ? v : null),
  );

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsWeekdaysOptions({
      query: {
        ...filters.channelQuery,
        From: period.from,
        To: period.to,
        Measure: measure,
        Scale: scale,
      },
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });
  // Follows the response, not the toggle: keepPreviousData shows the old scale until the new one arrives
  const format = data?.scale === "weekShare" ? formatPercent : formatAverage;
  const units = data?.measure === "units";

  return (
    <SummaryCard
      title="По дням недели"
      isFetching={isFetching}
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
      <Stack spacing={1.5}>
        <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={measure}
            onChange={(_, value: AnalyticsMeasure | null) => value && setMeasure(value)}
          >
            <ToggleButton value="orders">Заказы</ToggleButton>
            <ToggleButton value="units">Штуки</ToggleButton>
          </ToggleButtonGroup>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={scale}
            onChange={(_, value: WeekdayScale | null) => value && setScale(value)}
          >
            <ToggleButton value="average">Среднее</ToggleButton>
            <ToggleButton value="weekShare">Доля недели</ToggleButton>
          </ToggleButtonGroup>
        </Stack>

        {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
        {data &&
          (data.rows.length === 0 ? (
            <Typography color="text.secondary">Нет выбранных каналов</Typography>
          ) : data.scale === "weekShare" && data.fullWeeks === 0 ? (
            <Typography color="text.secondary">
              В периоде нет ни одной полной завершённой недели (Пн–Вс)
            </Typography>
          ) : (
            <Stack spacing={1}>
              <Box sx={{overflowX: "auto"}}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell />
                      {WEEKDAYS.map((day) => (
                        <TableCell key={day} align="center">
                          {day}
                        </TableCell>
                      ))}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    <HeatRow
                      label="Все каналы"
                      values={data.total}
                      color={theme.palette.primary.main}
                      format={format}
                      bold
                    />
                    {data.rows.map((row) => (
                      <HeatRow
                        key={row.tagId ?? row.marketplaceAccountId ?? row.kind}
                        label={<ChannelChip row={row} />}
                        values={row.values}
                        color={channelColor(row.marketplaceAccountId)}
                        format={format}
                        nested={row.kind === "directTag" || row.kind === "directUntagged"}
                      />
                    ))}
                  </TableBody>
                </Table>
              </Box>
              <Typography variant="caption" color="text.secondary">
                {data.scale === "weekShare"
                  ? `Медианная доля дня в недельных ${units ? "штуках" : "заказах"} по ${pluralCount(data.fullWeeks, NOUNS.fullWeek)} Пн–Вс`
                  : `${units ? "Штук" : "Заказов"} в среднем за один такой день периода`}
                {data.countedTo && data.countedTo < data.to
                  ? ` — по ${formatDateOnly(data.countedTo)}, сегодняшний и следующие дни не считаются`
                  : ""}
                .
                {data.scale === "weekShare" &&
                  " Неполные недели по краям периода и недели без продаж не считаются, поэтому сумма строки не обязательно 100%."}{" "}
                Цвет идёт от самого слабого дня строки к самому сильному.
                {data.rows.some((r) => r.kind === "directTag") &&
                  " Заказ с несколькими тегами входит в строку каждого из них."}
              </Typography>
            </Stack>
          ))}
      </Stack>
    </SummaryCard>
  );
}

export default WeekdaysCard;
