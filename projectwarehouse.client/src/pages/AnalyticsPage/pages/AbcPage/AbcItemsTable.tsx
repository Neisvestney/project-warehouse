import {
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
import type {AbcClass, AbcDto, XyzClass} from "@/api/types.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import CatalogItemLink from "@/components/catalog/CatalogItemLink";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import DataTableContainer from "@/components/DataTableContainer";
import SearchInput from "@/components/SearchInput";
import TableRowEmpty from "@/components/TableRowEmpty";
import {BASIS_LABELS, formatAbcValue} from "./abcClasses";
import {AbcChip, XyzChip} from "./ClassChips";

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
}: AbcItemsTableProps) {
  const openCatalogItem = useOpenCatalogItem();
  const {items, basis, currencyCode} = data;
  const classFilter = `${abcClass?.toUpperCase() ?? ""}${xyzClass?.toUpperCase() ?? ""}`;
  const valueLabel =
    basis === "units"
      ? "Штуки"
      : `${BASIS_LABELS[basis]}${currencyCode ? `, ${currencyCode}` : ""}`;

  return (
    <Stack spacing={1.5}>
      <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1.5}}>
        <Typography variant="subtitle1" sx={{fontWeight: 600}}>
          Позиции
        </Typography>
        {classFilter && (
          <Chip
            size="small"
            variant="outlined"
            label={`Фильтр: ${classFilter}`}
            onDelete={onClearClass}
          />
        )}
        <SearchInput value={search} onChange={onSearchChange} sx={{ml: "auto", width: 240}} />
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
              <TableCell>Позиция</TableCell>
              <TableCell>Тип</TableCell>
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
                <TableRow key={item.catalogItemId}>
                  <TableCell sx={{color: "text.secondary"}}>{item.rank}</TableCell>
                  <TableCell sx={{wordBreak: "break-word", minWidth: 200}}>
                    <CatalogItemLink catalogItemId={item.catalogItemId} onOpen={openCatalogItem}>
                      <Typography variant="body2">{item.name}</Typography>
                    </CatalogItemLink>
                  </TableCell>
                  <TableCell>
                    <CatalogItemTypeChip type={item.type} />
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
