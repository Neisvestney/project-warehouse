import {Box, Stack, Typography, useTheme} from "@mui/material";
import {BarPlot} from "@mui/x-charts/BarChart";
import {ChartsAxisHighlight} from "@mui/x-charts/ChartsAxisHighlight";
import {ChartsDataProvider} from "@mui/x-charts/ChartsDataProvider";
import {ChartsGrid} from "@mui/x-charts/ChartsGrid";
import {ChartsReferenceLine} from "@mui/x-charts/ChartsReferenceLine";
import {ChartsSurface} from "@mui/x-charts/ChartsSurface";
import {ChartsTooltip} from "@mui/x-charts/ChartsTooltip";
import {ChartsWrapper} from "@mui/x-charts/ChartsWrapper";
import {ChartsYAxis} from "@mui/x-charts/ChartsYAxis";
import {LinePlot} from "@mui/x-charts/LineChart";
import type {AbcClass, AbcDto} from "@/api/types.gen";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";
import {ABC_CLASSES, formatAbcValue, formatBoundary} from "@/components/analytics/abc/abcClasses";

interface ParetoChartProps {
  data: AbcDto;
  height: number;
}

/**
 * One bar per analysed item in rank order, painted by class, with the cumulative share over them. Items can
 * run into hundreds, so the bars carry no labels: the tooltip names the place and the numbers.
 */
function ParetoChart({data, height}: ParetoChartProps) {
  const theme = useTheme();
  const {pareto, totalValue, basis, currencyCode} = data;
  const {abcBoundaryA, abcBoundaryB} = data.settings;

  // The CSS variable follows the active scheme; `theme.palette` holds the light one's static value
  const curveColor = theme.vars?.palette.text.primary ?? theme.palette.text.primary;
  const colors: Record<AbcClass, string> = {
    a: theme.palette.success.main,
    b: theme.palette.warning.main,
    c: theme.palette.grey[500],
  };

  const cumulative = cumulativeShares(
    pareto.map((p) => p.value),
    totalValue,
  );
  const format = (v: number) => formatAbcValue(v, basis, currencyCode);

  return (
    <Stack spacing={1}>
      <ChartsDataProvider
        height={height}
        margin={{left: 8, right: 8, top: 8, bottom: 8}}
        xAxis={[
          {
            id: "x",
            scaleType: "band",
            data: pareto.map((_, i) => i),
            categoryGapRatio: pareto.length > 60 ? 0 : 0.2,
            valueFormatter: (i: number) => `№ ${formatNumber(i + 1)}`,
          },
        ]}
        yAxis={[
          {id: "value", min: 0, width: 56, valueFormatter: (v: number) => format(v)},
          {
            id: "share",
            position: "right",
            min: 0,
            max: 1,
            width: 48,
            valueFormatter: (v: number) => formatPercent(v),
          },
        ]}
        series={[
          // One series per class stacked together, so each band is filled by its own class alone
          ...ABC_CLASSES.map((c) => ({
            type: "bar" as const,
            id: c,
            label: c.toUpperCase(),
            stack: "abc",
            yAxisId: "value",
            color: colors[c],
            data: pareto.map((p) => (p.class === c ? p.value : null)),
            valueFormatter: (v: number | null) => (v == null ? null : format(v)),
          })),
          {
            type: "line" as const,
            id: "cumulative",
            label: "Накопленная доля",
            yAxisId: "share",
            color: curveColor,
            curve: "linear" as const,
            showMark: false,
            data: cumulative,
            valueFormatter: (v: number | null) => (v == null ? null : formatPercent(v)),
          },
        ]}
      >
        <ChartsWrapper>
          <ChartsSurface>
            <ChartsGrid horizontal />
            <BarPlot />
            <ChartsReferenceLine
              y={abcBoundaryA / 100}
              axisId="share"
              lineStyle={{stroke: colors.a, strokeDasharray: "4 3"}}
            />
            <ChartsReferenceLine
              y={abcBoundaryB / 100}
              axisId="share"
              lineStyle={{stroke: colors.b, strokeDasharray: "4 3"}}
            />
            <LinePlot />
            <ChartsAxisHighlight x="band" />
            <ChartsYAxis axisId="value" />
            <ChartsYAxis axisId="share" />
          </ChartsSurface>
          <ChartsTooltip trigger="axis" />
        </ChartsWrapper>
      </ChartsDataProvider>

      <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 2, px: 1}}>
        {ABC_CLASSES.map((c) => (
          <LegendItem key={c} color={colors[c]} label={c.toUpperCase()} />
        ))}
        <LegendItem color={curveColor} label="Накопленная доля — правая шкала" line />
        <Typography variant="caption" color="text.secondary">
          пунктир — границы A {formatBoundary(abcBoundaryA)} и B {formatBoundary(abcBoundaryB)}
        </Typography>
      </Stack>
    </Stack>
  );
}

function cumulativeShares(values: number[], total: number): number[] {
  const result: number[] = [];
  let running = 0;
  for (const value of values) {
    running += value;
    result.push(total > 0 ? running / total : 0);
  }
  return result;
}

function LegendItem({color, label, line}: {color: string; label: string; line?: boolean}) {
  return (
    <Stack direction="row" spacing={0.75} sx={{alignItems: "center"}}>
      <Box sx={{width: line ? 14 : 10, height: line ? 3 : 10, borderRadius: 0.5, bgcolor: color}} />
      <Typography variant="caption">{label}</Typography>
    </Stack>
  );
}

export default ParetoChart;
