import type {ReactNode} from "react";
import {ButtonBase, Skeleton, Stack, Toolbar, Typography} from "@mui/material";

export interface TableInfoStat {
  key: string;
  label: string;
  value: ReactNode;
  /** Theme color of the value, e.g. `error.main` for overdue counters. */
  color?: string;
  /** Keeps the stat out of the bar, e.g. a zero counter that carries no meaning. */
  hidden?: boolean;
  /** Turns the stat into a toggle, e.g. a counter that filters the list down to what it counts. */
  onClick?: () => void;
  /** Marks a toggle stat as applied. */
  active?: boolean;
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
        // on a phone the stats wrap as whole pairs instead of breaking a label in two
        flexWrap: "wrap",
        columnGap: 2,
        rowGap: 0.5,
        py: {xs: 1, sm: 0},
        whiteSpace: "nowrap",
      }}
    >
      {visible.map((stat) => {
        const content = (
          <>
            <Typography variant="body2">{stat.label}</Typography>
            {/* the skeleton sits inside the Typography so both states share one line box and baseline */}
            <Typography
              variant="body2"
              sx={{fontWeight: 600, color: stat.color ?? "text.primary", minWidth: 32}}
            >
              {loading ? <Skeleton variant="text" sx={{transform: "none"}} /> : stat.value}
            </Typography>
          </>
        );

        return stat.onClick ? (
          <ButtonBase
            key={stat.key}
            onClick={stat.onClick}
            aria-pressed={stat.active ?? false}
            sx={{
              gap: 0.5,
              alignItems: "baseline",
              px: 1,
              mx: -1,
              py: 0.25,
              borderRadius: 1,
              border: 1,
              borderColor: stat.active ? (stat.color ?? "primary.main") : "transparent",
              backgroundColor: stat.active ? "action.selected" : undefined,
              "&:hover": {backgroundColor: "action.hover"},
            }}
          >
            {content}
          </ButtonBase>
        ) : (
          <Stack key={stat.key} direction="row" spacing={0.5} sx={{alignItems: "baseline"}}>
            {content}
          </Stack>
        );
      })}
      <Stack direction="row" spacing={1} sx={{alignItems: "center", ml: "auto"}}>
        {children}
      </Stack>
    </Toolbar>
  );
}

export default TableInfoBar;
