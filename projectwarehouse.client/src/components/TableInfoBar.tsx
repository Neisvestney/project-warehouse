import type {ReactNode} from "react";
import {Skeleton, Stack, Toolbar, Typography} from "@mui/material";

export interface TableInfoStat {
  key: string;
  label: string;
  value: ReactNode;
  /** Theme color of the value, e.g. `error.main` for overdue counters. */
  color?: string;
  /** Keeps the stat out of the bar, e.g. a zero counter that carries no meaning. */
  hidden?: boolean;
}

interface TableInfoBarProps {
  stats: TableInfoStat[];
  /** Replaces the values with placeholders while the list is loading. */
  loading?: boolean;
  /** Pushed to the right end of the bar. */
  children?: ReactNode;
}

/**
 * Summary of the whole list shown above a table. Matches the dense height of BulkBar, which replaces it
 * while rows are selected.
 */
function TableInfoBar({stats, loading, children}: TableInfoBarProps) {
  const visible = stats.filter((s) => !s.hidden);

  return (
    <Toolbar
      variant="dense"
      sx={{
        color: "text.secondary",
        border: 1,
        borderColor: "divider",
        borderRadius: 1,
        gap: 2,
      }}
    >
      {visible.map((stat) => (
        <Stack key={stat.key} direction="row" spacing={0.5} sx={{alignItems: "baseline"}}>
          <Typography variant="body2">{stat.label}</Typography>
          {/* the skeleton sits inside the Typography so both states share one line box and baseline */}
          <Typography
            variant="body2"
            sx={{fontWeight: 600, color: stat.color ?? "text.primary", minWidth: 32}}
          >
            {loading ? <Skeleton variant="text" sx={{transform: "none"}} /> : stat.value}
          </Typography>
        </Stack>
      ))}
      <Stack direction="row" spacing={1} sx={{alignItems: "center", ml: "auto"}}>
        {children}
      </Stack>
    </Toolbar>
  );
}

export default TableInfoBar;
