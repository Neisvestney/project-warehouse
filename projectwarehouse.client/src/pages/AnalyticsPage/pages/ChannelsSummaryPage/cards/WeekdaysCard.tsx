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
import type {AnalyticsMeasure} from "@/api/types.gen";
import {useSyncedWithQueryState} from "@/hooks/useSyncedWithQueryState";
import {formatDateOnly} from "@/utils/dateOnly";
import {extractErrorMessage} from "@/utils/errorUtils";
import ChannelChip from "../ChannelChip";
import {parseMeasure} from "../channelsQuery";
import {formatAverage} from "../channelsSummaryUtils";
import {useChannelColor} from "../charts/useChannelColor";
import PeriodPicker from "../period/PeriodPicker";
import {CARD_PERIOD_PRESETS, useCardPeriod} from "../period/usePeriodParam";
import SummaryCard from "../SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";

const WEEKDAYS = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

interface HeatRowProps {
  label: ReactNode;
  values: (number | null)[];
  color: string;
  bold?: boolean;
  nested?: boolean;
}

/**
 * Intensity runs from the row's weakest day to its strongest: scaled from zero, a week of 88…99 would come
 * out one flat tone, and Direct next to a big shop would fade into a pale strip.
 */
function HeatRow({label, values, color, bold, nested}: HeatRowProps) {
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
            {value == null ? "—" : formatAverage(value)}
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

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetChannelsWeekdaysOptions({
      query: {...filters.channelQuery, From: period.from, To: period.to, Measure: measure},
    }),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  return (
    <SummaryCard
      title="По дням недели"
      isFetching={isFetching}
      actions={
        <>
          <ToggleButtonGroup
            exclusive
            size="small"
            value={measure}
            onChange={(_, value: AnalyticsMeasure | null) => value && setMeasure(value)}
          >
            <ToggleButton value="orders">Заказы</ToggleButton>
            <ToggleButton value="units">Штуки</ToggleButton>
          </ToggleButtonGroup>
          <PeriodPicker
            variant="select"
            presets={CARD_PERIOD_PRESETS}
            value={selection}
            onChange={setSelection}
            pagePeriod={pagePeriod}
          />
        </>
      }
    >
      {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
      {data &&
        (data.rows.length === 0 ? (
          <Typography color="text.secondary">Нет выбранных каналов</Typography>
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
                    bold
                  />
                  {data.rows.map((row) => (
                    <HeatRow
                      key={row.tagId ?? row.marketplaceAccountId ?? row.kind}
                      label={<ChannelChip row={row} />}
                      values={row.values}
                      color={channelColor(row.marketplaceAccountId)}
                      nested={row.kind === "directTag" || row.kind === "directUntagged"}
                    />
                  ))}
                </TableBody>
              </Table>
            </Box>
            <Typography variant="caption" color="text.secondary">
              {measure === "units" ? "Штук" : "Заказов"} в среднем за один такой день периода
              {data.countedTo && data.countedTo < data.to
                ? ` — по ${formatDateOnly(data.countedTo)}, сегодняшний и следующие дни не считаются`
                : ""}
              . Цвет идёт от самого слабого дня строки к самому сильному.
              {data.rows.some((r) => r.kind === "directTag") &&
                " Заказ с несколькими тегами входит в строку каждого из них."}
            </Typography>
          </Stack>
        ))}
    </SummaryCard>
  );
}

export default WeekdaysCard;
