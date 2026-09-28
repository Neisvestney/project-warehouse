import {
  Button,
  Chip,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import CalendarViewMonthIcon from "@mui/icons-material/CalendarViewMonth";
import type {AbcClass, AbcDto, XyzClass} from "@/api/types.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import DataTableContainer from "@/components/DataTableContainer";
import SearchInput from "@/components/SearchInput";
import TableRowEmpty from "@/components/TableRowEmpty";
import {BASIS_LABELS, formatAbcValue} from "@/components/analytics/abc/abcClasses";
import {AbcChip, XyzChip} from "@/components/analytics/abc/ClassChips";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import {SUBJECT_LABELS} from "./abcSubjects";
import AbcSubjectName from "./AbcSubjectName";

const COLUMNS = 9;

interface AbcItemsTableProps {
  data: AbcDto;
  /** Only the table's own page is on its way — paging, search or a class filter. */
  isFetching: boolean;
  abcClass: AbcClass | null;
  xyzClass: XyzClass | null;
  onClearClass: () => void;
  search: string;
  onSearchChange: (value: string) => void;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  /** Opens the month-by-month classes of the same rows. */
  onShowTimeline: () => void;
}

function AbcItemsTable({
  data,
  isFetching,
  abcClass,
  xyzClass,
  onClearClass,
  search,
  onSearchChange,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  onShowTimeline,
}: AbcItemsTableProps) {
  const {items, basis, subject, currencyCode} = data;
  const labels = SUBJECT_LABELS[subject];
  const classFilter = `${abcClass?.toUpperCase() ?? ""}${xyzClass?.toUpperCase() ?? ""}`;
  const valueLabel =
    basis === "units"
      ? "Штуки"
      : `${BASIS_LABELS[basis]}${currencyCode ? `, ${currencyCode}` : ""}`;

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1.5}}>
        <Typography variant="subtitle1" sx={{fontWeight: 600}}>
          {labels.title}
        </Typography>
        {classFilter && (
          <Chip
            size="small"
            variant="outlined"
            label={`Фильтр: ${classFilter}`}
            onDelete={onClearClass}
          />
        )}
        <Button
          size="small"
          startIcon={<CalendarViewMonthIcon />}
          onClick={onShowTimeline}
          disabled={items.total === 0}
          sx={{ml: "auto"}}
        >
          По месяцам
        </Button>
        <SearchInput value={search} onChange={onSearchChange} sx={{width: 240}} />
      </Stack>

      <DataTableContainer
        variant="outlined"
        isFetching={isFetching}
        count={items.total}
        page={page}
        onPageChange={onPageChange}
        rowsPerPage={pageSize}
        onRowsPerPageChange={onPageSizeChange}
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell sx={{width: 40}}>#</TableCell>
              <TableCell>{labels.column}</TableCell>
              <TableCell>{subject === "catalogItem" ? "Тип" : "Магазин"}</TableCell>
              <TableCell align="right">{valueLabel}</TableCell>
              <TableCell align="right">Доля</TableCell>
              <TableCell align="right">Накопл.</TableCell>
              <TableCell>ABC</TableCell>
              <TableCell>XYZ</TableCell>
              <TableCell align="right">Вариация</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.items.length === 0 ? (
              <TableRowEmpty
                colSpan={COLUMNS}
                message={
                  search || classFilter ? "Ничего не найдено" : "Продаж товаров за период нет"
                }
              />
            ) : (
              items.items.map((item) => (
                <TableRow key={item.subject.key}>
                  <TableCell sx={{color: "text.secondary"}}>{item.rank}</TableCell>
                  <TableCell sx={{wordBreak: "break-word", minWidth: 200}}>
                    <AbcSubjectName subject={item.subject} />
                  </TableCell>
                  <TableCell>
                    {item.subject.type ? (
                      <CatalogItemTypeChip type={item.subject.type} />
                    ) : (
                      <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 0.5}}>
                        {item.subject.accounts.map((a) => (
                          <MarketplaceAccountChip
                            key={a.id}
                            accountId={a.id}
                            name={a.name}
                            type={a.type}
                            search={`?tab=cards&search=${encodeURIComponent(item.subject.offerId ?? "")}`}
                          />
                        ))}
                      </Stack>
                    )}
                  </TableCell>
                  <TableCell align="right" sx={{whiteSpace: "nowrap"}}>
                    {formatAbcValue(item.value, basis, currencyCode)}
                  </TableCell>
                  <TableCell align="right">{formatPercent(item.share)}</TableCell>
                  <TableCell align="right">{formatPercent(item.cumulativeShare)}</TableCell>
                  <TableCell>
                    <AbcChip value={item.abcClass} />
                  </TableCell>
                  <TableCell>
                    <XyzChip value={item.xyzClass} />
                  </TableCell>
                  <TableCell align="right">
                    {item.xyzIntervals != null ? (
                      <Tooltip title={`По ${item.xyzIntervals} полным интервалам`}>
                        <span>{formatPercent(item.cv)}</span>
                      </Tooltip>
                    ) : (
                      formatPercent(item.cv)
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </DataTableContainer>
    </Stack>
  );
}

export default AbcItemsTable;
