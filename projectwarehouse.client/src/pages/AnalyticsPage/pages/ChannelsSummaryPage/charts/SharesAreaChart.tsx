import {LineChart} from "@mui/x-charts/LineChart";
import type {AnalyticsIntervalDto, AnalyticsStep, ChannelSeriesDto} from "@/api/types.gen";
import {formatNumber, formatPercent} from "../channelsSummaryUtils";
import {intervalLabel, seriesLabel} from "./intervalLabels";
import {useChannelColor} from "./useChannelColor";

interface SharesAreaChartProps {
  intervals: AnalyticsIntervalDto[];
  series: ChannelSeriesDto[];
  step: AnalyticsStep;
  height: number;
}

/**
 * Each interval filled to 100% and split between the channels. The shares are taken here rather than left
 * to a stack offset, so an interval with no sales stays empty instead of dividing by zero.
 */
function SharesAreaChart({intervals, series, step, height}: SharesAreaChartProps) {
  const channelColor = useChannelColor();
  const totals = intervals.map((_, i) => series.reduce((sum, s) => sum + (s.values[i] ?? 0), 0));

  return (
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
      yAxis={[{min: 0, max: 1, width: 48, valueFormatter: (v: number) => formatPercent(v)}]}
      series={series.map((s) => ({
        id: s.marketplaceAccountId ?? s.kind,
        label: seriesLabel(s),
        color: channelColor(s.marketplaceAccountId),
        stack: "shares",
        area: true,
        curve: "linear",
        showMark: false,
        data: s.values.map((v, i) => (v == null || totals[i] === 0 ? null : v / totals[i])),
        valueFormatter: (share, {dataIndex}) => {
          const value = s.values[dataIndex];
          return share == null || value == null
            ? null
            : `${formatPercent(share)} (${formatNumber(value)})`;
        },
      }))}
    />
  );
}

export default SharesAreaChart;
