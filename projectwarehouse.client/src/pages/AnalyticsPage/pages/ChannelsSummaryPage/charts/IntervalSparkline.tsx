import {Box} from "@mui/material";
import type {AnalyticsIntervalDto, AnalyticsStep} from "@/api/types.gen";
import {formatNumber} from "@/components/analytics/analyticsFormat";
import {intervalLabel, isIncomplete} from "@/components/analytics/charts/intervalLabels";

interface IntervalSparklineProps {
  values: (number | null)[];
  intervals: AnalyticsIntervalDto[];
  step: AnalyticsStep;
  /** Intervals drawn pale besides the incomplete ones, e.g. whose returns are still coming in. */
  pale?: boolean[];
  /** The scale's top; defaults to the row's own maximum. */
  max?: number;
  width?: number;
  height?: number;
}

/** One bar per interval, its exact value in the native tooltip of the bar. */
function IntervalSparkline({
  values,
  intervals,
  step,
  pale,
  max,
  width = 120,
  height = 28,
}: IntervalSparklineProps) {
  const top = max ?? Math.max(1, ...values.map((v) => v ?? 0));
  const slot = width / Math.max(1, values.length);
  const gap = slot > 4 ? 1 : 0;

  return (
    <Box
      component="svg"
      viewBox={`0 0 ${width} ${height}`}
      sx={{width, height, display: "block", color: "primary.main", flexShrink: 0}}
    >
      {values.map((value, i) => {
        const interval = intervals[i];
        const barHeight = value ? Math.max(1.5, (value / top) * height) : 0;
        const faded = isIncomplete(interval) || pale?.[i];
        return (
          <g key={interval.start}>
            <title>{`${intervalLabel(interval, step, i === 0)}: ${value == null ? "—" : formatNumber(value)}`}</title>
            <rect x={i * slot} y={0} width={slot} height={height} fill="transparent" />
            <rect
              x={i * slot + gap / 2}
              y={height - barHeight}
              width={Math.max(0.5, slot - gap)}
              height={barHeight}
              fill="currentColor"
              opacity={faded ? 0.4 : 1}
            />
          </g>
        );
      })}
      <line
        x1={0}
        x2={width}
        y1={height - 0.5}
        y2={height - 0.5}
        stroke="currentColor"
        strokeOpacity={0.2}
      />
    </Box>
  );
}

export default IntervalSparkline;
