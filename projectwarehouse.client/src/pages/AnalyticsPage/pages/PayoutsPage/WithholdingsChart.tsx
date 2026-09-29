import {Box, Stack, Typography, alpha, useTheme} from "@mui/material";
import {BarPlot} from "@mui/x-charts/BarChart";
import {ChartsAxisHighlight} from "@mui/x-charts/ChartsAxisHighlight";
import {ChartsDataProvider} from "@mui/x-charts/ChartsDataProvider";
import {ChartsGrid} from "@mui/x-charts/ChartsGrid";
import {ChartsSurface} from "@mui/x-charts/ChartsSurface";
import {ChartsTooltip} from "@mui/x-charts/ChartsTooltip";
import {ChartsWrapper} from "@mui/x-charts/ChartsWrapper";
import {ChartsXAxis} from "@mui/x-charts/ChartsXAxis";
import {ChartsYAxis} from "@mui/x-charts/ChartsYAxis";
import {LinePlot} from "@mui/x-charts/LineChart";
import type {AnalyticsIntervalDto, AnalyticsStep, PayoutsWithholdingsDto} from "@/api/types.gen";
import {formatCompact, formatMoney, formatPercent} from "@/components/analytics/analyticsFormat";
import {intervalLabel, isIncomplete} from "@/components/analytics/charts/intervalLabels";
import {withheldShare} from "./withholdings";

const DASHED_PREFIX = "dashed:";

interface WithholdingsChartProps {
  intervals: AnalyticsIntervalDto[];
  step: AnalyticsStep;
  withholdings: PayoutsWithholdingsDto;
  currencyCode: string;
  height: number;
}

/** Sales as bars on the left axis, the two withheld shares as lines on the right one. */
function WithholdingsChart({
  intervals,
  step,
  withholdings,
  currencyCode,
  height,
}: WithholdingsChartProps) {
  const theme = useTheme();
  const {sales, withheldByPosting, withheldByShop} = withholdings;

  const salesColor = alpha(theme.palette.primary.main, 0.35);
  const incompleteSalesColor = alpha(theme.palette.primary.main, 0.12);
  const postingColor = theme.palette.warning.main;
  const shopColor = theme.palette.error.main;

  const incomplete = intervals.map(isIncomplete);
  // The dashed piece spans the neighbours too, so it joins the solid line instead of floating apart
  const nearIncomplete = (i: number) => incomplete[i] || incomplete[i - 1] || incomplete[i + 1];

  const lineSeries = (id: string, label: string, color: string, withheld: (number | null)[]) => {
    const shares = withheld.map((w, i) => withheldShare(w, sales[i]));
    const common = {
      type: "line" as const,
      yAxisId: "share",
      color,
      curve: "linear" as const,
      showMark: false,
    };
    return [
      {
        ...common,
        id,
        label,
        data: shares.map((v, i) => (incomplete[i] ? null : v)),
        valueFormatter: (_: number | null, {dataIndex}: {dataIndex: number}) => {
          const v = shares[dataIndex];
          return v == null
            ? null
            : `${formatPercent(v)} · ${formatMoney(-(withheld[dataIndex] ?? 0), currencyCode)}`;
        },
      },
      {
        ...common,
        id: `${DASHED_PREFIX}${id}`,
        disableHighlight: true,
        data: shares.map((v, i) => (nearIncomplete(i) ? v : null)),
        valueFormatter: () => null,
      },
    ];
  };

  return (
    <Stack spacing={1}>
      <ChartsDataProvider
        height={height}
        margin={{left: 8, right: 8, top: 8, bottom: 8}}
        xAxis={[
          {
            id: "x",
            scaleType: "band",
            data: intervals.map((_, i) => i),
            valueFormatter: (i: number) =>
              intervals[i] ? intervalLabel(intervals[i], step, i === 0) : "",
          },
        ]}
        yAxis={[
          {id: "money", width: 56, valueFormatter: (v: number) => formatCompact(v)},
          {
            id: "share",
            position: "right",
            min: 0,
            width: 48,
            valueFormatter: (v: number) => formatPercent(v),
          },
        ]}
        series={[
          {
            type: "bar",
            id: "sales",
            label: "Продажи",
            yAxisId: "money",
            color: salesColor,
            data: sales,
            valueFormatter: (v) => (v == null ? null : formatMoney(v, currencyCode)),
          },
          ...lineSeries("byPosting", "Удержано по отправлениям", postingColor, withheldByPosting),
          ...lineSeries("byShop", "Удержано по магазину", shopColor, withheldByShop),
        ]}
      >
        <ChartsWrapper>
          <ChartsSurface
            sx={{
              [`& .MuiLineChart-line[data-series^="${DASHED_PREFIX}"]`]: {strokeDasharray: "5 4"},
            }}
          >
            <ChartsGrid horizontal />
            <BarPlot
              slotProps={{
                bar: ({dataIndex}) =>
                  incomplete[dataIndex]
                    ? {fill: incompleteSalesColor, stroke: salesColor, strokeDasharray: "4 3"}
                    : {},
              }}
            />
            <LinePlot />
            <ChartsAxisHighlight x="band" />
            <ChartsXAxis axisId="x" />
            <ChartsYAxis axisId="money" />
            <ChartsYAxis axisId="share" />
          </ChartsSurface>
          <ChartsTooltip trigger="axis" />
        </ChartsWrapper>
      </ChartsDataProvider>

      <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 2, px: 1}}>
        <LegendItem color={salesColor} label="Продажи — левая шкала" />
        <LegendItem color={postingColor} label="Удержано по отправлениям, % от продаж" line />
        <LegendItem color={shopColor} label="Удержано по магазину, % от продаж" line />
        {incomplete.some(Boolean) && (
          <Typography variant="caption" color="text.secondary">
            пунктир и * — неполный или ещё идущий интервал
          </Typography>
        )}
      </Stack>
    </Stack>
  );
}

function LegendItem({color, label, line}: {color: string; label: string; line?: boolean}) {
  return (
    <Stack direction="row" spacing={0.75} sx={{alignItems: "center"}}>
      <Box sx={{width: line ? 14 : 10, height: line ? 3 : 10, borderRadius: 0.5, bgcolor: color}} />
      <Typography variant="caption">{label}</Typography>
    </Stack>
  );
}

export default WithholdingsChart;
