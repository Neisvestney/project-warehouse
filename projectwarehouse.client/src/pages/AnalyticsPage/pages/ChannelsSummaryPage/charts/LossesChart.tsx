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
import type {ChannelsLossesDto} from "@/api/types.gen";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";
import {intervalLabel} from "@/components/analytics/charts/intervalLabels";

const DASHED = "returns:immature";

function share(part: number | null | undefined, whole: number | null | undefined) {
  return part == null || !whole ? null : part / whole;
}

interface LossesChartProps {
  data: ChannelsLossesDto;
  height: number;
}

/**
 * Sales as bars on the left axis, the cancellation and return shares as lines on the right one: in counts
 * both lines would only repeat the shape of the bars. The return line turns dashed where the interval's
 * returns are still coming in, the way an unfinished interval does on the other charts.
 */
function LossesChart({data, height}: LossesChartProps) {
  const theme = useTheme();
  const {points, step} = data;
  const hasShops = points.some((p) => p.shopUnits != null);

  const salesColor = alpha(theme.palette.primary.main, 0.35);
  const cancelColor = theme.palette.warning.main;
  const returnColor = theme.palette.error.main;

  const cancelShares = points.map((p) =>
    share(p.cancellations, (p.saleOrders ?? 0) + (p.cancellations ?? 0)),
  );
  const returnShares = points.map((p) => share(p.returnedUnits, p.shopUnits));
  const immature = points.map((p) => p.returnsImmature);
  // The dashed piece starts one point early so it joins the solid line instead of floating apart
  const nearImmature = (i: number) => immature[i] || immature[i + 1];
  const salesLabel = data.measure === "units" ? "Продано, шт." : "Продано, заказов";

  return (
    <Stack spacing={1}>
      <ChartsDataProvider
        height={height}
        margin={{left: 8, right: 8, top: 8, bottom: 8}}
        xAxis={[
          {
            id: "x",
            scaleType: "band",
            data: points.map((_, i) => i),
            valueFormatter: (i: number) =>
              points[i] ? intervalLabel(points[i].interval, step, i === 0) : "",
          },
        ]}
        yAxis={[
          {id: "count", min: 0, width: 48, valueFormatter: (v: number) => formatNumber(v)},
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
            label: salesLabel,
            yAxisId: "count",
            color: salesColor,
            data: points.map((p) => p.sales ?? null),
            valueFormatter: (v) => (v == null ? null : formatNumber(v)),
          },
          {
            type: "line",
            id: "cancellations",
            label: "Отмены",
            yAxisId: "share",
            color: cancelColor,
            curve: "linear",
            showMark: false,
            data: cancelShares,
            valueFormatter: (v, {dataIndex}) =>
              v == null
                ? null
                : `${formatPercent(v)} · ${formatNumber(points[dataIndex].cancellations ?? 0)} отм.`,
          },
          ...(hasShops
            ? [
                {
                  type: "line" as const,
                  id: "returns",
                  label: "Возвраты",
                  yAxisId: "share",
                  color: returnColor,
                  curve: "linear" as const,
                  showMark: false,
                  data: returnShares.map((v, i) => (immature[i] ? null : v)),
                  valueFormatter: (_: number | null, {dataIndex}: {dataIndex: number}) => {
                    const v = returnShares[dataIndex];
                    if (v == null) return null;
                    const text = `${formatPercent(v)} · ${formatNumber(points[dataIndex].returnedUnits ?? 0)} шт.`;
                    return immature[dataIndex] ? `${text}, ещё поступают` : text;
                  },
                },
                {
                  type: "line" as const,
                  id: DASHED,
                  yAxisId: "share",
                  color: returnColor,
                  curve: "linear" as const,
                  showMark: false,
                  disableHighlight: true,
                  data: returnShares.map((v, i) => (nearImmature(i) ? v : null)),
                  valueFormatter: () => null,
                },
              ]
            : []),
        ]}
      >
        <ChartsWrapper>
          <ChartsSurface
            sx={{[`& .MuiLineChart-line[data-series="${DASHED}"]`]: {strokeDasharray: "5 4"}}}
          >
            <ChartsGrid horizontal />
            <BarPlot />
            <LinePlot />
            <ChartsAxisHighlight x="band" />
            <ChartsXAxis axisId="x" />
            <ChartsYAxis axisId="count" />
            <ChartsYAxis axisId="share" />
          </ChartsSurface>
          <ChartsTooltip trigger="axis" />
        </ChartsWrapper>
      </ChartsDataProvider>

      <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 2, px: 1}}>
        <LegendItem color={salesColor} label={`${salesLabel} — левая шкала`} />
        <LegendItem color={cancelColor} label="Отмены, % от заказов" line />
        {hasShops && <LegendItem color={returnColor} label="Возвраты, % от проданных штук" line />}
        {hasShops && immature.some(Boolean) && (
          <Typography variant="caption" color="text.secondary">
            пунктир — возвраты ещё поступают: интервал закончился меньше {data.returnsMaturityDays}{" "}
            дн. назад
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

export default LossesChart;
