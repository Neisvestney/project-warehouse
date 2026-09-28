import type {ReactNode} from "react";
import {
  Box,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import type {ChannelsSummaryDto, ChannelSummaryRowDto} from "@/api/types.gen";
import {formatDateOnly} from "@/utils/dateOnly";
import ChannelChip from "@/components/analytics/ChannelChip";
import {formatMoney, formatNumber, formatPercent} from "@/components/analytics/analyticsFormat";

interface Column {
  label: string;
  render: (row: ChannelSummaryRowDto, summary: ChannelsSummaryDto) => ReactNode;
}

function Muted({children}: {children: ReactNode}) {
  return (
    <Typography component="span" variant="caption" color="text.secondary">
      {children}
    </Typography>
  );
}

function PerCurrency({lines}: {lines: {key: string; node: ReactNode}[]}) {
  if (lines.length === 0) return <>—</>;
  return (
    <Stack>
      {lines.map((line) => (
        <Box key={line.key} sx={{whiteSpace: "nowrap"}}>
          {line.node}
        </Box>
      ))}
    </Stack>
  );
}

/** A count with its share under it: side by side the two numbers read as one. */
function ValueWithRate({value, rate}: {value: ReactNode; rate?: ReactNode}) {
  return (
    <Stack sx={{alignItems: "flex-end"}}>
      <Box sx={{whiteSpace: "nowrap"}}>{value}</Box>
      {rate && (
        <Typography variant="caption" color="text.secondary" sx={{whiteSpace: "nowrap"}}>
          {rate}
        </Typography>
      )}
    </Stack>
  );
}

function isMarketplace(row: ChannelSummaryRowDto) {
  return row.kind === "marketplace";
}

const COLUMNS: Column[] = [
  {
    label: "Заказы",
    render: (row) => formatNumber(row.orders),
  },
  {
    label: "Δ",
    render: (row, summary) => {
      const delta = row.orders - row.previousOrders;
      const sign = delta > 0 ? "+" : "";
      return (
        <Tooltip
          title={`К периоду ${formatDateOnly(summary.previousFrom)} — ${formatDateOnly(summary.previousTo)}: ${formatNumber(row.previousOrders)}`}
        >
          <Box
            sx={{
              color: delta > 0 ? "success.main" : delta < 0 ? "error.main" : "text.secondary",
            }}
          >
            <ValueWithRate
              value={`${sign}${formatNumber(delta)}`}
              rate={row.previousOrders > 0 && `${sign}${formatPercent(delta / row.previousOrders)}`}
            />
          </Box>
        </Tooltip>
      );
    },
  },
  {
    label: "Штуки",
    render: (row) => formatNumber(row.units),
  },
  {
    label: "Отмены",
    render: (row) => (
      <ValueWithRate
        value={formatNumber(row.cancellations)}
        rate={row.cancellationRate != null && formatPercent(row.cancellationRate)}
      />
    ),
  },
  {
    label: "Возвраты",
    render: (row, summary) =>
      row.returnedUnits == null ? (
        "—"
      ) : (
        <ValueWithRate
          value={formatNumber(row.returnedUnits)}
          rate={
            row.returnRate != null && (
              <>
                {formatPercent(row.returnRate)}
                {summary.returnsImmature && (
                  <Tooltip
                    title={`Возвраты ещё поступают: период закончился меньше ${summary.returnsMaturityDays} дн. назад`}
                  >
                    <Box component="span" sx={{color: "warning.main", cursor: "help"}}>
                      *
                    </Box>
                  </Tooltip>
                )}
              </>
            )
          }
        />
      ),
  },
  {
    label: "Выручка",
    render: (row, summary) =>
      !isMarketplace(row) ? (
        "—"
      ) : (
        <Stack>
          <PerCurrency
            lines={row.money.map((m) => ({
              key: m.currencyCode,
              node: formatMoney(m.revenue, m.currencyCode),
            }))}
          />
          {summary.moneyMode === "payout" && (
            <Muted>покрытие {formatPercent(row.payoutCoverage)}</Muted>
          )}
        </Stack>
      ),
  },
  {
    label: "Средний чек",
    render: (row) =>
      !isMarketplace(row) ? (
        "—"
      ) : (
        <PerCurrency
          lines={row.money.map((m) => ({
            key: m.currencyCode,
            node: m.averageCheck == null ? "—" : formatMoney(m.averageCheck, m.currencyCode),
          }))}
        />
      ),
  },
  {
    label: "Скидка",
    render: (row) =>
      !isMarketplace(row) ? (
        "—"
      ) : (
        <PerCurrency
          lines={row.money.map((m) => ({
            key: m.currencyCode,
            node: (
              <Tooltip title={`Сумма скидок: ${formatMoney(m.discountAmount, m.currencyCode)}`}>
                <span>{formatPercent(m.discountDepth)}</span>
              </Tooltip>
            ),
          }))}
        />
      ),
  },
  {
    label: "Доля",
    render: (row) => (
      <Stack>
        <Box sx={{whiteSpace: "nowrap"}}>
          {formatPercent(row.unitsShare)} <Muted>шт.</Muted>
        </Box>
        {row.money.map((m) => (
          <Box key={m.currencyCode} sx={{whiteSpace: "nowrap"}}>
            {formatPercent(m.revenueShare)} <Muted>{m.currencyCode}</Muted>
          </Box>
        ))}
      </Stack>
    ),
  },
];

function rowKey(row: ChannelSummaryRowDto) {
  return `${row.kind}:${row.marketplaceAccountId ?? row.tagId ?? ""}`;
}

function isNested(row: ChannelSummaryRowDto) {
  return row.kind === "directTag" || row.kind === "directUntagged";
}

interface ChannelsSummaryTableProps {
  summary: ChannelsSummaryDto;
}

function ChannelsSummaryTable({summary}: ChannelsSummaryTableProps) {
  const theme = useTheme();
  const isNarrow = useMediaQuery(theme.breakpoints.down("sm"));

  if (summary.rows.length === 0)
    return (
      <Paper variant="outlined" sx={{p: 3, textAlign: "center"}}>
        <Typography color="text.secondary">Нет выбранных каналов</Typography>
      </Paper>
    );

  if (isNarrow)
    return (
      <Stack spacing={1}>
        {summary.rows.map((row) => (
          <Paper key={rowKey(row)} variant="outlined" sx={{p: 1.5, ml: isNested(row) ? 2 : 0}}>
            <ChannelChip row={row} />
            <Box
              sx={{
                mt: 1,
                display: "grid",
                gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
                gap: 1,
              }}
            >
              {COLUMNS.map((column) => (
                <Box key={column.label}>
                  <Typography variant="caption" color="text.secondary" component="div">
                    {column.label}
                  </Typography>
                  <Typography variant="body2" component="div">
                    {column.render(row, summary)}
                  </Typography>
                </Box>
              ))}
            </Box>
          </Paper>
        ))}
      </Stack>
    );

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Канал</TableCell>
            {COLUMNS.map((column) => (
              <TableCell key={column.label} align="right">
                {column.label}
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {summary.rows.map((row) => (
            <TableRow key={rowKey(row)} hover>
              <TableCell sx={{pl: isNested(row) ? 5 : undefined}}>
                <ChannelChip row={row} />
              </TableCell>
              {COLUMNS.map((column) => (
                <TableCell key={column.label} align="right" sx={{verticalAlign: "top"}}>
                  {column.render(row, summary)}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

export default ChannelsSummaryTable;
