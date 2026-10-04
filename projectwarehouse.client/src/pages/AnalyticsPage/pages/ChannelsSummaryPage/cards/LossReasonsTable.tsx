import {Fragment, useState} from "react";
import {
  Box,
  Collapse,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import type {ChannelsLossReasonsDto, LossReasonRowDto} from "@/api/types.gen";
import {formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";
import {intervalLabel} from "@/components/analytics/charts/intervalLabels";
import SubjectName from "@/components/analytics/subjects/SubjectName";
import {SUBJECT_LABELS} from "@/components/analytics/subjects/subjects";
import IntervalSparkline from "../charts/IntervalSparkline";

const NO_REASON = "Причина не указана";

function rowKey(row: LossReasonRowDto) {
  return `${row.subject.key}|${row.reason ?? ""}`;
}

/** The first interval with the most units; null when the row lost nothing in any interval. */
function peakIndex(values: (number | null)[]) {
  let best: number | null = null;
  values.forEach((v, i) => {
    if (v && (best == null || v > (values[best] ?? 0))) best = i;
  });
  return best;
}

function ShareCell({row}: {row: LossReasonRowDto}) {
  return (
    <Tooltip title={`${formatNumber(row.units)} из ${formatNumber(row.baseUnits)} шт.`}>
      <span>{formatPercent(row.share)}</span>
    </Tooltip>
  );
}

/** Units of each interval the row lost anything in, for the expanded row. */
function IntervalBreakdown({data, row}: {data: ChannelsLossReasonsDto; row: LossReasonRowDto}) {
  return (
    <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", columnGap: 2, rowGap: 0.5}}>
      {row.values.map((value, i) =>
        value ? (
          <Typography key={data.intervals[i].start} variant="caption" sx={{whiteSpace: "nowrap"}}>
            <Box component="span" sx={{color: "text.secondary"}}>
              {intervalLabel(data.intervals[i], data.step, i === 0)}
              {data.immatureIntervals[i] && " (ещё поступают)"}:
            </Box>{" "}
            {formatNumber(value)} шт.
          </Typography>
        ) : null,
      )}
    </Stack>
  );
}

interface LossReasonsTableProps {
  data: ChannelsLossReasonsDto;
}

function LossReasonsTable({data}: LossReasonsTableProps) {
  const theme = useTheme();
  const narrow = useMediaQuery(theme.breakpoints.down("sm"));
  const [expanded, setExpanded] = useState<string | null>(null);
  const toggle = (key: string) => setExpanded((current) => (current === key ? null : key));
  // One scale for every row, so a tall bar means many units rather than this row's own peak
  const max = Math.max(1, ...data.rows.flatMap((r) => r.values.map((v) => v ?? 0)));

  const peakLabel = (row: LossReasonRowDto) => {
    const peak = peakIndex(row.values);
    return peak == null ? "—" : intervalLabel(data.intervals[peak], data.step, peak === 0);
  };

  const sparkline = (row: LossReasonRowDto) => (
    <IntervalSparkline
      values={row.values}
      intervals={data.intervals}
      step={data.step}
      pale={data.immatureIntervals}
      max={max}
    />
  );

  if (narrow)
    return (
      <Stack spacing={1}>
        {data.rows.map((row, i) => {
          const key = rowKey(row);
          return (
            <Paper
              key={key}
              variant="outlined"
              sx={{p: 1.5, cursor: "pointer"}}
              onClick={() => toggle(key)}
            >
              <Box onClick={(e) => e.stopPropagation()}>
                <SubjectName subject={row.subject} prefix={i + 1} showAccounts />
              </Box>
              <Typography variant="body2" sx={{mt: 0.5}}>
                {row.reason ?? NO_REASON}
              </Typography>
              <Stack direction="row" spacing={2} sx={{mt: 1, alignItems: "center"}}>
                <Box>
                  <Typography variant="caption" color="text.secondary" component="div">
                    Штук
                  </Typography>
                  {formatNumber(row.units)}
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary" component="div">
                    Доля
                  </Typography>
                  <ShareCell row={row} />
                </Box>
                <Box sx={{ml: "auto"}}>{sparkline(row)}</Box>
              </Stack>
              <Collapse in={expanded === key} unmountOnExit>
                <Box sx={{mt: 1}}>
                  <IntervalBreakdown data={data} row={row} />
                </Box>
              </Collapse>
            </Paper>
          );
        })}
      </Stack>
    );

  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell sx={{width: 32}}>#</TableCell>
          <TableCell>{SUBJECT_LABELS[data.subject].column}</TableCell>
          <TableCell>Причина</TableCell>
          <TableCell align="right">Штук</TableCell>
          <TableCell align="right">Доля</TableCell>
          <TableCell>По интервалам</TableCell>
          <TableCell>Пик</TableCell>
          <TableCell sx={{width: 40}} />
        </TableRow>
      </TableHead>
      <TableBody>
        {data.rows.map((row, i) => {
          const key = rowKey(row);
          const open = expanded === key;
          return (
            <Fragment key={key}>
              <TableRow
                hover
                onClick={() => toggle(key)}
                sx={{cursor: "pointer", "& > td": open ? {borderBottom: "none"} : undefined}}
              >
                <TableCell sx={{color: "text.secondary"}}>{i + 1}</TableCell>
                <TableCell sx={{wordBreak: "break-word"}} onClick={(e) => e.stopPropagation()}>
                  <SubjectName subject={row.subject} showAccounts />
                </TableCell>
                <TableCell sx={{wordBreak: "break-word"}}>
                  {row.reason ?? (
                    <Box component="span" sx={{color: "text.secondary"}}>
                      {NO_REASON}
                    </Box>
                  )}
                </TableCell>
                <TableCell align="right">{formatNumber(row.units)}</TableCell>
                <TableCell align="right">
                  <ShareCell row={row} />
                </TableCell>
                <TableCell>{sparkline(row)}</TableCell>
                <TableCell sx={{whiteSpace: "nowrap"}}>{peakLabel(row)}</TableCell>
                <TableCell>
                  <IconButton size="small" aria-label={open ? "Свернуть" : "Развернуть"}>
                    {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                  </IconButton>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell colSpan={8} sx={{py: 0, ...(open ? {} : {borderBottom: "none"})}}>
                  <Collapse in={open} unmountOnExit>
                    <Box sx={{py: 1}}>
                      <IntervalBreakdown data={data} row={row} />
                    </Box>
                  </Collapse>
                </TableCell>
              </TableRow>
            </Fragment>
          );
        })}
      </TableBody>
    </Table>
  );
}

export default LossReasonsTable;
