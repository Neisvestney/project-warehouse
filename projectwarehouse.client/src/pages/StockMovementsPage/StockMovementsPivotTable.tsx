import {useEffect, useMemo, useRef} from "react";
import {
  Box,
  Button,
  CircularProgress,
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
import type {
  CatalogItemSelectDto,
  StockMovementPivotRowDto,
  StockMovementTotalsDto,
} from "@/api/types.gen";
import TableRowEmpty from "@/components/TableRowEmpty";
import TableRowLoader from "@/components/TableRowLoader";
import {formatDateOnly, formatWeekday, isWeekend} from "./dateOnly";

const EMPTY_TOTALS: StockMovementTotalsDto = {
  inQuantity: 0,
  outQuantity: 0,
  transferInQuantity: 0,
  transferOutQuantity: 0,
  movementsCount: 0,
  net: 0,
};

function sumTotals(totals: StockMovementTotalsDto[]): StockMovementTotalsDto {
  return totals.reduce(
    (acc, t) => ({
      inQuantity: acc.inQuantity + t.inQuantity,
      outQuantity: acc.outQuantity + t.outQuantity,
      transferInQuantity: acc.transferInQuantity + t.transferInQuantity,
      transferOutQuantity: acc.transferOutQuantity + t.transferOutQuantity,
      movementsCount: acc.movementsCount + t.movementsCount,
      net: acc.net + t.net,
    }),
    EMPTY_TOTALS,
  );
}

/**
 * `stickyHeader` pins every `th` at `top: 0`, so a second header row has to be offset by hand — and
 * that offset only stays right if the first row's height is fixed rather than content-driven.
 */
const HEADER_ROW_HEIGHT = 56;

const stickyColumnSx = {
  position: "sticky",
  left: 0,
  backgroundColor: "background.paper",
  borderRight: 1,
  borderRightColor: "divider",
} as const;

function MovementCell({
  totals,
  showTransfers,
}: {
  totals: StockMovementTotalsDto | undefined;
  showTransfers: boolean;
}) {
  const hasDirect = !!totals && (totals.inQuantity > 0 || totals.outQuantity > 0);
  const hasTransfers =
    !!totals && (totals.transferInQuantity > 0 || totals.transferOutQuantity > 0);

  if (!hasDirect && !(showTransfers && hasTransfers)) {
    return (
      <Typography variant="body2" sx={{color: "text.disabled"}}>
        —
      </Typography>
    );
  }

  return (
    <Stack sx={{alignItems: "center"}}>
      <Stack direction="row" spacing={0.75}>
        {totals!.inQuantity > 0 && (
          <Typography variant="body2" sx={{color: "success.main", fontWeight: 500}}>
            +{totals!.inQuantity}
          </Typography>
        )}
        {totals!.outQuantity > 0 && (
          <Typography variant="body2" sx={{color: "error.main", fontWeight: 500}}>
            −{totals!.outQuantity}
          </Typography>
        )}
        {!hasDirect && (
          <Typography variant="body2" sx={{color: "text.disabled"}}>
            —
          </Typography>
        )}
      </Stack>
      {showTransfers && hasTransfers && (
        <Typography variant="caption" sx={{color: "info.main"}} title="Перемещения">
          ⇄ +{totals!.transferInQuantity} −{totals!.transferOutQuantity}
        </Typography>
      )}
    </Stack>
  );
}

interface StockMovementsPivotTableProps {
  columns: CatalogItemSelectDto[];
  rows: StockMovementPivotRowDto[];
  showTransfers: boolean;
  isLoading: boolean;
  isFetching: boolean;
  isFetchingNextPage: boolean;
  hasNextPage: boolean;
  onLoadMore: () => void;
}

function StockMovementsPivotTable({
  columns,
  rows,
  showTransfers,
  isLoading,
  isFetching,
  isFetchingNextPage,
  hasNextPage,
  onLoadMore,
}: StockMovementsPivotTableProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const loadMoreRef = useRef<HTMLTableCellElement>(null);

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

  const columnTotals = useMemo(() => {
    const byItem = new Map<string, StockMovementTotalsDto[]>();
    rows.forEach((row) =>
      row.cells.forEach((cell) => {
        const bucket = byItem.get(cell.catalogItemId);
        if (bucket) bucket.push(cell);
        else byItem.set(cell.catalogItemId, [cell]);
      }),
    );
    return new Map([...byItem].map(([id, cells]) => [id, sumTotals(cells)]));
  }, [rows]);

  const grandTotal = useMemo(() => sumTotals(rows.map((row) => row.total)), [rows]);

  const colSpan = columns.length + 2;
  const loadedRangeLabel =
    rows.length > 0
      ? `${formatDateOnly(rows[rows.length - 1].date)} — ${formatDateOnly(rows[0].date)}`
      : null;

  return (
    <Paper>
      <LinearProgress
        sx={{visibility: isFetching ? "visible" : "hidden", borderRadius: "4px 4px 0 0"}}
      />
      <TableContainer ref={containerRef} sx={{maxHeight: "70vh"}}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell
                sx={{...stickyColumnSx, zIndex: 4, minWidth: 150, height: HEADER_ROW_HEIGHT}}
              >
                Дата
              </TableCell>
              {columns.map((item) => (
                <TableCell
                  key={item.id}
                  align="center"
                  sx={{minWidth: 130, maxWidth: 220, height: HEADER_ROW_HEIGHT}}
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
              <TableCell align="center" sx={{minWidth: 130, height: HEADER_ROW_HEIGHT}}>
                Итого за день
              </TableCell>
            </TableRow>
            <TableRow>
              <TableCell sx={{...stickyColumnSx, zIndex: 4, top: HEADER_ROW_HEIGHT}}>
                <Typography variant="body2" sx={{fontWeight: 500}}>
                  Итого
                </Typography>
                {loadedRangeLabel && (
                  <Typography variant="caption" sx={{color: "text.secondary"}}>
                    {loadedRangeLabel}
                  </Typography>
                )}
              </TableCell>
              {columns.map((item) => (
                <TableCell key={item.id} align="center" sx={{top: HEADER_ROW_HEIGHT}}>
                  <MovementCell totals={columnTotals.get(item.id)} showTransfers={showTransfers} />
                </TableCell>
              ))}
              <TableCell align="center" sx={{top: HEADER_ROW_HEIGHT}}>
                <MovementCell totals={grandTotal} showTransfers={showTransfers} />
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isLoading ? (
              <TableRowLoader colSpan={colSpan} />
            ) : rows.length === 0 ? (
              <TableRowEmpty colSpan={colSpan} message="Движений за период не найдено" />
            ) : (
              rows.map((row) => {
                const cells = new Map(row.cells.map((cell) => [cell.catalogItemId, cell]));
                return (
                  <TableRow key={row.date} hover>
                    <TableCell sx={stickyColumnSx}>
                      <Typography
                        variant="body2"
                        sx={{color: isWeekend(row.date) ? "text.secondary" : "text.primary"}}
                      >
                        {formatDateOnly(row.date)}
                      </Typography>
                      <Typography variant="caption" sx={{color: "text.secondary"}}>
                        {formatWeekday(row.date)}
                      </Typography>
                    </TableCell>
                    {columns.map((item) => (
                      <TableCell key={item.id} align="center">
                        <MovementCell totals={cells.get(item.id)} showTransfers={showTransfers} />
                      </TableCell>
                    ))}
                    <TableCell align="center">
                      <MovementCell totals={row.total} showTransfers={showTransfers} />
                    </TableCell>
                  </TableRow>
                );
              })
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
        </Table>
      </TableContainer>
    </Paper>
  );
}

export default StockMovementsPivotTable;
