import type {ReactNode} from "react";
import {
  Alert,
  Box,
  Dialog,
  IconButton,
  LinearProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import type {AnalyticsIntervalDto, AnalyticsStep} from "@/api/types.gen";
import {extractErrorMessage} from "@/utils/errorUtils";
import ChannelsSelect from "../ChannelsSelect";
import ChannelsLineChart from "../charts/ChannelsLineChart";
import {type ChartSeries, formatCount, type ValueFormat} from "../charts/chartSeries";
import {intervalLabel, seriesLabel} from "../charts/intervalLabels";
import {useChannelColor} from "../charts/useChannelColor";
import PeriodPicker from "../period/PeriodPicker";
import {PERIOD_PRESETS} from "../period/periodSelection";
import type {useChartFullscreen} from "./useChartFullscreen";

export interface ChartData {
  intervals: AnalyticsIntervalDto[];
  series: ChartSeries[];
  step: AnalyticsStep;
  timeZoneId: string;
}

interface ChartFullscreenDialogProps {
  open: boolean;
  state: ReturnType<typeof useChartFullscreen>;
  title: string;
  /** Step and measure toggles of the chart. */
  toggles: ReactNode;
  withDirect: boolean;
  /** How the chart and the table read a value; counts by default. */
  format?: ValueFormat;
  data: ChartData | undefined;
  isFetching: boolean;
  error: unknown;
}

// Esc and the focus trap come from the Dialog. `useBackClosable` cannot help: the open flag is synced to the
// URL with a replace, which would wipe its history marker and leave Back to reopen what was just closed.
function ChartFullscreenDialog({
  open,
  state,
  title,
  toggles,
  withDirect,
  format = formatCount,
  data,
  isFetching,
  error,
}: ChartFullscreenDialogProps) {
  return (
    <Dialog fullScreen open={open} onClose={state.close}>
      <Stack
        direction="row"
        useFlexGap
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          px: 3,
          py: 1.25,
          borderBottom: 1,
          borderColor: "divider",
        }}
      >
        <Typography variant="h6" sx={{fontWeight: 400}}>
          {title}
        </Typography>

        <PeriodPicker
          variant="toggles"
          presets={PERIOD_PRESETS}
          value={state.selection}
          onChange={state.setSelection}
          pagePeriod={state.period}
        />

        {toggles}

        <IconButton sx={{ml: "auto"}} onClick={state.close} title="Свернуть (Esc)">
          <CloseIcon />
        </IconButton>
      </Stack>

      <Box sx={{height: 2}}>{isFetching && <LinearProgress sx={{height: 2}} />}</Box>

      <Stack spacing={2} sx={{p: 3, pt: 2, overflow: "auto", flexGrow: 1}}>
        <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 2}}>
          <ChannelsSelect
            value={state.channels}
            onChange={state.setChannels}
            withDirect={withDirect}
          />
          <Typography variant="caption" color="text.secondary">
            Период графика не зависит от периода страницы
            {data && ` · сутки по ${data.timeZoneId}`}
          </Typography>
        </Stack>

        {error != null && <Alert severity="error">{extractErrorMessage(error)}</Alert>}

        {data && (
          <>
            <Paper variant="outlined" sx={{p: 2}}>
              <ChannelsLineChart
                intervals={data.intervals}
                series={data.series}
                step={data.step}
                height={420}
                format={format}
              />
            </Paper>
            <IntervalTable data={data} format={format} />
          </>
        )}
      </Stack>
    </Dialog>
  );
}

function IntervalTable({data, format}: {data: ChartData; format: ValueFormat}) {
  const channelColor = useChannelColor();

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{position: "sticky", left: 0, bgcolor: "background.paper", zIndex: 1}}>
              Канал
            </TableCell>
            {data.intervals.map((interval, i) => (
              <TableCell key={interval.start} align="right" sx={{whiteSpace: "nowrap"}}>
                {intervalLabel(interval, data.step, i === 0)}
              </TableCell>
            ))}
            <TableCell align="right" sx={{fontWeight: 600}}>
              Итого
            </TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {data.series.map((series) => (
            <TableRow key={series.marketplaceAccountId ?? series.kind} hover>
              <TableCell
                sx={{
                  position: "sticky",
                  left: 0,
                  bgcolor: "background.paper",
                  zIndex: 1,
                  whiteSpace: "nowrap",
                }}
              >
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Box
                    sx={{
                      width: 10,
                      height: 10,
                      borderRadius: 0.5,
                      bgcolor: channelColor(series.marketplaceAccountId),
                    }}
                  />
                  <span>{seriesLabel(series)}</span>
                </Stack>
              </TableCell>
              {series.values.map((value, i) => (
                <TableCell
                  key={data.intervals[i].start}
                  align="right"
                  sx={{color: value == null ? "text.disabled" : undefined}}
                >
                  {value == null ? "—" : format(value)}
                </TableCell>
              ))}
              <TableCell align="right" sx={{fontWeight: 600}}>
                {series.total == null ? "—" : format(series.total)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

export default ChartFullscreenDialog;
