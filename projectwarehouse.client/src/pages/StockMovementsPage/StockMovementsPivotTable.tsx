import {Fragment, useEffect, useId, useRef, useState} from "react";
import {useVirtualizer} from "@tanstack/react-virtual";
import {
  Box,
  Button,
  CircularProgress,
  LinearProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableFooter,
  TableHead,
  TableRow,
  Typography,
  useTheme,
} from "@mui/material";
import type {
  CatalogItemSelectDto,
  StockMovementMetricDto,
  StockMovementPivotRowDto,
} from "@/api/types.gen";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {formatDateOnly, formatWeekday, isWeekend} from "@/utils/dateOnly";

/**
 * `stickyHeader` pins every `th` at `top: 0`, so the second header row has to be offset by the height of
 * the first. That height is content-driven — a group cell holds a name and an article — and `height` on a
 * `th` is only a minimum, so the offset is measured rather than assumed; a stale one parks the metric row
 * over the group row and the column borders visibly merge on scroll.
 */
const GROUP_ROW_MIN_HEIGHT = 44;
const METRIC_ROW_HEIGHT = 130;

/** Estimate only — the virtualizer measures each row's actual rendered height. */
const DATA_ROW_HEIGHT_ESTIMATE = 48;
const ROW_OVERSCAN = 8;

/** Two fixed sub-columns close every group: the raw net, then the stock left at the end of the day. */
const FIXED_COLUMNS = 2;

/**
 * The layout is `fixed` with explicit widths: under `auto` every column sizes to the rows currently
 * mounted, and the virtualizer swapping rows in and out would make the columns jump while scrolling.
 */
const DATE_COLUMN_WIDTH = 170;
const VALUE_COLUMN_WIDTH = 64;

const stickyColumnSx = {
  position: "sticky",
  left: 0,
  backgroundColor: "background.paper",
  borderRight: 2,
  borderRightColor: "divider",
} as const;

/** A group boundary has to read differently from a metric boundary, or 30 columns turn to mush. */
const groupStartSx = {borderLeft: 2, borderLeftColor: "divider"} as const;
const metricStartSx = {borderLeft: 1, borderLeftColor: "divider"} as const;

function signColor(value: number): string {
  if (value > 0) return "success.main";
  if (value < 0) return "error.main";
  return "text.disabled";
}

function formatSigned(value: number): string {
  if (value === 0) return "—";
  return value > 0 ? `+${value}` : `−${Math.abs(value)}`;
}

const valueCellSx = {px: 1, overflow: "hidden", textOverflow: "ellipsis"} as const;
const clickableSx = {cursor: "pointer"} as const;

interface ValueCellProps {
  col: number;
  onClick?: () => void;
}

function NumberCell({
  value,
  first,
  bold,
  col,
  onClick,
}: ValueCellProps & {value: number; first: boolean; bold?: boolean}) {
  return (
    <TableCell
      align="right"
      data-col={col}
      onClick={onClick}
      sx={{
        ...(first ? groupStartSx : metricStartSx),
        ...valueCellSx,
        ...(onClick && clickableSx),
      }}
    >
      <Typography
        variant="body2"
        noWrap
        sx={{
          color: signColor(value),
          fontWeight: bold ? 600 : 400,
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {formatSigned(value)}
      </Typography>
    </TableCell>
  );
}

function BalanceCell({value, col, onClick}: ValueCellProps & {value: number | undefined}) {
  return (
    <TableCell
      align="right"
      data-col={col}
      onClick={onClick}
      sx={{
        ...metricStartSx,
        ...valueCellSx,
        ...(onClick && clickableSx),
        backgroundColor: "action.hover",
      }}
    >
      <Typography
        variant="body2"
        noWrap
        sx={{
          fontVariantNumeric: "tabular-nums",
          color: value === undefined ? "text.disabled" : undefined,
        }}
      >
        {value ?? "—"}
      </Typography>
    </TableCell>
  );
}

function MetricHeadCell({
  label,
  first,
  top,
  col,
}: {
  label: string;
  first: boolean;
  top: number;
  col: number;
}) {
  return (
    <TableCell
      align="center"
      data-col={col}
      sx={{
        ...(first ? groupStartSx : metricStartSx),
        top,
        height: METRIC_ROW_HEIGHT,
        px: 0.5,
        verticalAlign: "bottom",
      }}
    >
      <Typography
        variant="caption"
        sx={{
          writingMode: "vertical-rl",
          transform: "rotate(180deg)",
          whiteSpace: "nowrap",
          fontWeight: 500,
          maxHeight: METRIC_ROW_HEIGHT - 20,
          overflow: "hidden",
        }}
        title={label}
      >
        {label}
      </Typography>
    </TableCell>
  );
}

/** A clicked body cell. `catalogItemId` is null for the total group, `metricIndex` for net and balance. */
export interface PivotCellRef {
  date: string;
  catalogItemId: string | null;
  metricIndex: number | null;
}

interface StockMovementsPivotTableProps {
  columns: CatalogItemSelectDto[];
  metrics: StockMovementMetricDto[];
  rows: StockMovementPivotRowDto[];
  /** Infinite mode loads arbitrary windows, so a «за период» total would name no period. */
  isInfinite: boolean;
  isLoading: boolean;
  isFetching: boolean;
  isFetchingNextPage: boolean;
  hasNextPage: boolean;
  onLoadMore: () => void;
  onCellClick: (cell: PivotCellRef) => void;
  /** Fills the wrapper instead of capping at 70vh — used by the full-tab dialog. */
  fill?: boolean;
}

function StockMovementsPivotTable({
  columns,
  metrics,
  rows,
  isInfinite,
  isLoading,
  isFetching,
  isFetchingNextPage,
  hasNextPage,
  onLoadMore,
  onCellClick,
  fill,
}: StockMovementsPivotTableProps) {
  "use no memo";

  const theme = useTheme();
  const tableId = useId();
  const containerRef = useRef<HTMLDivElement>(null);
  const hoverStyleRef = useRef<HTMLStyleElement>(null);
  const hoveredColRef = useRef<string | null>(null);
  const loadMoreRef = useRef<HTMLTableCellElement>(null);
  const groupRowRef = useRef<HTMLTableRowElement>(null);
  const [groupRowHeight, setGroupRowHeight] = useState(GROUP_ROW_MIN_HEIGHT);

  useEffect(() => {
    const row = groupRowRef.current;
    if (!row || typeof ResizeObserver === "undefined") return;

    const observer = new ResizeObserver(() => setGroupRowHeight(row.offsetHeight));
    observer.observe(row);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const target = loadMoreRef.current;
    const root = containerRef.current;
    if (!target || !root || !hasNextPage || isFetchingNextPage) return;
    if (typeof IntersectionObserver === "undefined") return;

    const observer = new IntersectionObserver(
      (entries) => entries[0]?.isIntersecting && onLoadMore(),
      {root, rootMargin: "300px"},
    );
    observer.observe(target);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, onLoadMore, rows.length]);

  // eslint-disable-next-line react-hooks/incompatible-library -- acknowledged: "use no memo" above covers it
  const rowVirtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => containerRef.current,
    estimateSize: () => DATA_ROW_HEIGHT_ESTIMATE,
    overscan: ROW_OVERSCAN,
    getItemKey: (index) => rows[index]?.date ?? index,
  });
  const virtualRows = rowVirtualizer.getVirtualItems();
  const paddingTop = virtualRows.length > 0 ? virtualRows[0].start : 0;
  const paddingBottom =
    virtualRows.length > 0
      ? rowVirtualizer.getTotalSize() - virtualRows[virtualRows.length - 1].end
      : 0;

  const groupWidth = metrics.length + FIXED_COLUMNS;
  const colSpan = 1 + groupWidth * (columns.length + 1);
  // Matches the placeholder `useStockMovementsPivot` sends for a blank name, so a column being renamed
  // is still labelled with whatever the server computed it under.
  const metricLabels = [
    ...metrics.map((m, index) => m.name.trim() || `#${index + 1}`),
    "Итого движение",
    "Остаток",
  ];

  const periodTotals = rows.reduce(
    (acc, row) => {
      metrics.forEach((_, i) => {
        acc.total[i] = (acc.total[i] ?? 0) + (row.total.metrics[i] ?? 0);
      });
      acc.totalNet += row.total.net;
      row.cells.forEach((cell) => {
        const bucket = (acc.byItem[cell.catalogItemId] ??= {metrics: [], net: 0});
        metrics.forEach((_, i) => {
          bucket.metrics[i] = (bucket.metrics[i] ?? 0) + (cell.metrics[i] ?? 0);
        });
        bucket.net += cell.net;
      });
      return acc;
    },
    {
      total: [] as number[],
      totalNet: 0,
      byItem: {} as Record<string, {metrics: number[]; net: number}>,
    },
  );

  const latest = rows[0];

  const valueColumnCount = groupWidth * (columns.length + 1);
  const tableWidth = DATE_COLUMN_WIDTH + valueColumnCount * VALUE_COLUMN_WIDTH;
  const columnHighlight = `color-mix(in srgb, ${theme.vars?.palette.action.hover ?? theme.palette.action.hover} 60%, transparent)`;

  // Written straight into a <style> rather than through state: a re-render per hovered cell would
  // re-render every mounted cell of a table that can be thousands of columns wide. A gradient overlays
  // whatever background a cell already has — sticky header, balance tint, the row hover.
  const setHoveredCol = (col: string | null) => {
    if (hoveredColRef.current === col || !hoverStyleRef.current) return;
    hoveredColRef.current = col;
    hoverStyleRef.current.textContent =
      col === null
        ? ""
        : `[data-pivot="${tableId}"] [data-col="${col}"] {background-image: linear-gradient(${columnHighlight}, ${columnHighlight});}`;
  };

  const cellClick =
    (date: string, catalogItemId: string | null, metricIndex: number | null) => () =>
      onCellClick({date, catalogItemId, metricIndex});

  return (
    <Paper
      sx={fill ? {display: "flex", flexDirection: "column", minHeight: 0, flex: 1} : undefined}
    >
      <LinearProgress
        sx={{visibility: isFetching ? "visible" : "hidden", borderRadius: "4px 4px 0 0"}}
      />
      <TableContainer ref={containerRef} sx={fill ? {flex: 1, minHeight: 0} : {maxHeight: "70vh"}}>
        <style ref={hoverStyleRef} />
        <Table
          size="small"
          stickyHeader
          data-pivot={tableId}
          onMouseOver={(e) =>
            setHoveredCol(
              (e.target as Element).closest("[data-col]")?.getAttribute("data-col") ?? null,
            )
          }
          onMouseLeave={() => setHoveredCol(null)}
          sx={{tableLayout: "fixed", width: tableWidth, minWidth: "100%"}}
        >
          <colgroup>
            <col style={{width: DATE_COLUMN_WIDTH}} />
            {Array.from({length: valueColumnCount}, (_, i) => (
              <col key={i} style={{width: VALUE_COLUMN_WIDTH}} />
            ))}
          </colgroup>
          <TableHead>
            <TableRow ref={groupRowRef}>
              <TableCell
                rowSpan={2}
                sx={{...stickyColumnSx, zIndex: 5, height: GROUP_ROW_MIN_HEIGHT}}
              >
                Дата
              </TableCell>

              <TableCell
                align="center"
                colSpan={groupWidth}
                sx={{...groupStartSx, height: GROUP_ROW_MIN_HEIGHT}}
              >
                <Typography variant="body2" sx={{fontWeight: 600}}>
                  Итого
                </Typography>
                <Typography variant="caption" sx={{color: "text.secondary"}}>
                  по выбранным позициям
                </Typography>
              </TableCell>

              {columns.map((item) => (
                <TableCell
                  key={item.id}
                  align="center"
                  colSpan={groupWidth}
                  sx={{...groupStartSx, height: GROUP_ROW_MIN_HEIGHT}}
                >
                  <Typography variant="body2" noWrap sx={{fontWeight: 500}} title={item.fullName}>
                    {item.fullName}
                  </Typography>
                  {item.article && (
                    <Typography variant="caption" noWrap sx={{color: "text.secondary"}}>
                      {item.article}
                    </Typography>
                  )}
                </TableCell>
              ))}
            </TableRow>

            <TableRow>
              {[{id: "total"}, ...columns].map((group, groupIndex) =>
                metricLabels.map((label, index) => (
                  <MetricHeadCell
                    key={`${group.id}-${index}`}
                    label={label}
                    first={index === 0}
                    top={groupRowHeight}
                    col={groupIndex * groupWidth + index}
                  />
                )),
              )}
            </TableRow>
          </TableHead>

          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={colSpan} />
            ) : rows.length === 0 ? (
              <TableRowEmpty colSpan={colSpan} message="Движений за период не найдено" />
            ) : (
              <>
                {paddingTop > 0 && (
                  <TableRow style={{height: paddingTop}}>
                    <TableCell colSpan={colSpan} sx={{p: 0, border: 0}} />
                  </TableRow>
                )}
                {virtualRows.map((virtualRow) => {
                  const row = rows[virtualRow.index];
                  const cells = new Map(row.cells.map((cell) => [cell.catalogItemId, cell]));
                  return (
                    <TableRow
                      key={row.date}
                      data-index={virtualRow.index}
                      ref={rowVirtualizer.measureElement}
                      hover
                    >
                      <TableCell sx={stickyColumnSx}>
                        <Typography
                          variant="body2"
                          noWrap
                          sx={{color: isWeekend(row.date) ? "text.secondary" : "text.primary"}}
                        >
                          {formatDateOnly(row.date)}
                        </Typography>
                        <Typography variant="caption" sx={{color: "text.secondary"}}>
                          {formatWeekday(row.date)}
                        </Typography>
                      </TableCell>

                      {metrics.map((metric, index) => (
                        <NumberCell
                          key={`total-${metric.name}-${index}`}
                          value={row.total.metrics[index] ?? 0}
                          first={index === 0}
                          col={index}
                          onClick={cellClick(row.date, null, index)}
                        />
                      ))}
                      <NumberCell
                        value={row.total.net}
                        first={metrics.length === 0}
                        bold
                        col={metrics.length}
                        onClick={cellClick(row.date, null, null)}
                      />
                      <BalanceCell
                        value={row.balance}
                        col={metrics.length + 1}
                        onClick={cellClick(row.date, null, null)}
                      />

                      {columns.map((item, itemIndex) => {
                        const cell = cells.get(item.id);
                        const offset = (itemIndex + 1) * groupWidth;
                        return (
                          <Fragment key={item.id}>
                            {metrics.map((metric, index) => (
                              <NumberCell
                                key={`${metric.name}-${index}`}
                                value={cell?.metrics[index] ?? 0}
                                first={index === 0}
                                col={offset + index}
                                onClick={cellClick(row.date, item.id, index)}
                              />
                            ))}
                            <NumberCell
                              value={cell?.net ?? 0}
                              first={metrics.length === 0}
                              bold
                              col={offset + metrics.length}
                              onClick={cellClick(row.date, item.id, null)}
                            />
                            <BalanceCell
                              value={cell?.balance}
                              col={offset + metrics.length + 1}
                              onClick={cellClick(row.date, item.id, null)}
                            />
                          </Fragment>
                        );
                      })}
                    </TableRow>
                  );
                })}
                {paddingBottom > 0 && (
                  <TableRow style={{height: paddingBottom}}>
                    <TableCell colSpan={colSpan} sx={{p: 0, border: 0}} />
                  </TableRow>
                )}
              </>
            )}

            {hasNextPage && !isLoading && (
              <TableRow>
                <TableCell ref={loadMoreRef} colSpan={colSpan} align="center">
                  {isFetchingNextPage ? (
                    <Box sx={{py: 1}}>
                      <CircularProgress size={20} />
                    </Box>
                  ) : (
                    <Button onClick={onLoadMore} size="small">
                      Загрузить ещё 30 дней
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            )}
          </TableBody>

          {!isInfinite && !isLoading && rows.length > 0 && (
            <TableFooter
              sx={{
                position: "sticky",
                bottom: 0,
                zIndex: 3,
                "& td": {
                  backgroundColor: "background.paper",
                  borderTop: 2,
                  borderTopColor: "divider",
                },
              }}
            >
              <TableRow>
                <TableCell sx={{...stickyColumnSx, zIndex: 4}}>
                  <Typography variant="body2" sx={{fontWeight: 600}}>
                    Итого за период
                  </Typography>
                </TableCell>

                {metrics.map((metric, index) => (
                  <NumberCell
                    key={`period-total-${metric.name}-${index}`}
                    value={periodTotals.total[index] ?? 0}
                    first={index === 0}
                    bold
                    col={index}
                  />
                ))}
                <NumberCell
                  value={periodTotals.totalNet}
                  first={metrics.length === 0}
                  bold
                  col={metrics.length}
                />
                <BalanceCell value={latest?.balance ?? 0} col={metrics.length + 1} />

                {columns.map((item, itemIndex) => {
                  const bucket = periodTotals.byItem[item.id];
                  const balance = latest?.cells.find((c) => c.catalogItemId === item.id)?.balance;
                  const offset = (itemIndex + 1) * groupWidth;
                  return (
                    <Fragment key={item.id}>
                      {metrics.map((metric, index) => (
                        <NumberCell
                          key={`${metric.name}-${index}`}
                          value={bucket?.metrics[index] ?? 0}
                          first={index === 0}
                          bold
                          col={offset + index}
                        />
                      ))}
                      <NumberCell
                        value={bucket?.net ?? 0}
                        first={metrics.length === 0}
                        bold
                        col={offset + metrics.length}
                      />
                      <BalanceCell value={balance} col={offset + metrics.length + 1} />
                    </Fragment>
                  );
                })}
              </TableRow>
            </TableFooter>
          )}
        </Table>
      </TableContainer>
    </Paper>
  );
}

export default StockMovementsPivotTable;
