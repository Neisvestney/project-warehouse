import {Box, Stack, Typography, useTheme} from "@mui/material";
import {LineChart, type LineSeries} from "@mui/x-charts/LineChart";
import type {AnalyticsIntervalDto, AnalyticsStep} from "@/api/types.gen";
import {useResolvedColorScheme} from "@/hooks/useResolvedColorScheme";
import {
  type ChartSeries,
  formatCount,
  TOTAL_LABEL,
  totalSeries,
  type TotalSeries,
  type ValueFormat,
} from "./chartSeries";
import {intervalLabel, isIncomplete, seriesLabel} from "./intervalLabels";
import {useChannelColor} from "./useChannelColor";

const DASHED_PREFIX = "dashed:";
const TOTAL_KEY = "total";

function seriesKey(series: ChartSeries): string {
  return series.marketplaceAccountId ?? series.kind;
}

/**
 * Each channel is drawn twice over the same points: a solid line through the complete intervals and a dashed
 * one covering only the incomplete edges and their neighbours, so a partial or running interval reads as
 * provisional. The dashed copy stays out of the tooltip and the legend.
 */
function toChartSeries(
  key: string,
  label: string,
  series: TotalSeries,
  intervals: AnalyticsIntervalDto[],
  color: string,
  format: ValueFormat,
): LineSeries[] {
  const incomplete = intervals.map(isIncomplete);
  const nearIncomplete = (i: number) => incomplete[i] || incomplete[i - 1] || incomplete[i + 1];

  return [
    {
      id: key,
      label,
      color,
      // Straight segments: a smoothed curve overshoots between points and draws values that never happened
      curve: "linear",
      showMark: false,
      data: series.values.map((v, i) => (incomplete[i] ? null : (v ?? null))),
      valueFormatter: (_, {dataIndex}) => {
        const value = series.values[dataIndex];
        return value == null ? null : format(value);
      },
    },
    {
      id: `${DASHED_PREFIX}${key}`,
      color,
      curve: "linear",
      showMark: false,
      disableHighlight: true,
      data: series.values.map((v, i) => (nearIncomplete(i) ? (v ?? null) : null)),
      valueFormatter: () => null,
    },
  ];
}

interface ChannelsLineChartProps {
  intervals: AnalyticsIntervalDto[];
  series: ChartSeries[];
  step: AnalyticsStep;
  height: number;
  /** How a value reads on the axis, in the tooltip and in the legend total; counts by default. */
  format?: ValueFormat;
  /** How a value reads on the axis when `format` is too wide for it; `format` by default. */
  axisFormat?: ValueFormat;
  /** Adds a line summing every channel; drawn only when there is more than one. */
  withTotal?: boolean;
}

function ChannelsLineChart({
  intervals,
  series,
  step,
  height,
  format = formatCount,
  axisFormat = format,
  withTotal = false,
}: ChannelsLineChartProps) {
  const channelColor = useChannelColor();
  const theme = useTheme();
  const {scheme} = useResolvedColorScheme();
  // `theme.palette` holds the light-scheme literals under cssVariables, and a chart stroke cannot take a CSS var
  const totalColor = scheme === "dark" ? theme.palette.common.white : theme.palette.common.black;
  const total = withTotal && series.length > 1 ? totalSeries(series) : null;
  // Counts start at zero; money can dip below it on a day of penalties
  const hasNegative = series.some((s) => s.values.some((v) => v != null && v < 0));

  if (series.length === 0)
    return (
      <Box sx={{height, display: "flex", alignItems: "center", justifyContent: "center"}}>
        <Typography color="text.secondary">Нет выбранных каналов</Typography>
      </Box>
    );

  return (
    <Stack spacing={1}>
      <LineChart
        height={height}
        hideLegend
        skipAnimation
        margin={{left: 8, right: 16, top: 8, bottom: 8}}
        xAxis={[
          {
            scaleType: "point",
            data: intervals.map((_, i) => i),
            valueFormatter: (i: number) =>
              intervals[i] ? intervalLabel(intervals[i], step, i === 0) : "",
          },
        ]}
        yAxis={[
          {
            min: hasNegative ? undefined : 0,
            width: 48,
            valueFormatter: (v: number) => axisFormat(v),
          },
        ]}
        series={[
          ...(total
            ? toChartSeries(TOTAL_KEY, TOTAL_LABEL, total, intervals, totalColor, format)
            : []),
          ...series.flatMap((s) =>
            toChartSeries(
              seriesKey(s),
              seriesLabel(s),
              s,
              intervals,
              channelColor(s.marketplaceAccountId),
              format,
            ),
          ),
        ]}
        sx={{
          [`& .MuiLineChart-line[data-series^="${DASHED_PREFIX}"]`]: {strokeDasharray: "5 4"},
        }}
      />
      <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 2, px: 1}}>
        {total && (
          <Stack direction="row" spacing={0.75} sx={{alignItems: "center"}}>
            <Box sx={{width: 10, height: 10, borderRadius: 0.5, bgcolor: totalColor}} />
            <Typography variant="caption" sx={{fontWeight: 600}}>
              {TOTAL_LABEL}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {total.total == null ? "—" : format(total.total)}
            </Typography>
          </Stack>
        )}
        {series.map((s) => (
          <Stack key={seriesKey(s)} direction="row" spacing={0.75} sx={{alignItems: "center"}}>
            <Box
              sx={{
                width: 10,
                height: 10,
                borderRadius: 0.5,
                bgcolor: channelColor(s.marketplaceAccountId),
              }}
            />
            <Typography variant="caption">{seriesLabel(s)}</Typography>
            <Typography variant="caption" color="text.secondary">
              {s.total == null ? "—" : format(s.total)}
            </Typography>
          </Stack>
        ))}
        {intervals.some(isIncomplete) && (
          <Typography variant="caption" color="text.secondary">
            пунктир и * — неполный или ещё идущий интервал
          </Typography>
        )}
      </Stack>
    </Stack>
  );
}

export default ChannelsLineChart;
