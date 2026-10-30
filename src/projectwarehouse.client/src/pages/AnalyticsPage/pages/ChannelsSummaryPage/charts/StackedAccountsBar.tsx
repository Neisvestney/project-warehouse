import {BarChart} from "@mui/x-charts/BarChart";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";

export interface StackPart<K extends string> {
  key: K;
  label: string;
  color: string;
}

export interface StackRow<K extends string> {
  id: string;
  label: string;
  values: Partial<Record<K, number | null>>;
}

interface StackedAccountsBarProps<K extends string> {
  rows: StackRow<K>[];
  parts: StackPart<K>[];
  share?: boolean;
}

const ROW_HEIGHT = 36;

/** One horizontal bar per shop split into parts; parts no shop has are left out of the legend. */
function StackedAccountsBar<K extends string>({
  rows,
  parts,
  share = false,
}: StackedAccountsBarProps<K>) {
  const present = parts.filter((p) => rows.some((r) => (r.values[p.key] ?? 0) > 0));
  const format = share ? formatPercent : formatNumber;

  return (
    <BarChart
      layout="horizontal"
      height={70 + rows.length * ROW_HEIGHT}
      skipAnimation
      margin={{left: 8, right: 16, top: 8, bottom: 8}}
      yAxis={[{scaleType: "band", data: rows.map((r) => r.label), width: 110}]}
      xAxis={[
        {min: 0, tickMinStep: share ? undefined : 1, valueFormatter: (v: number) => format(v)},
      ]}
      series={present.map((part) => ({
        id: part.key,
        label: part.label,
        color: part.color,
        stack: "total",
        data: rows.map((r) => r.values[part.key] ?? null),
        valueFormatter: (value: number | null) => (value == null ? null : format(value)),
      }))}
      slotProps={{legend: {position: {vertical: "bottom", horizontal: "start"}}}}
    />
  );
}

export default StackedAccountsBar;
