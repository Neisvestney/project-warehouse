import {useState} from "react";
import {
  Alert,
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  LinearProgress,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import type {ChipProps} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetAbcTimelineOptions} from "@/api/@tanstack/react-query.gen";
import type {AbcTimelineCellDto, AbcTimelineMonthDto} from "@/api/types.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import CatalogItemLink from "@/components/catalog/CatalogItemLink";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import {formatDateOnly, parseDateOnly} from "@/utils/dateOnly";
import {extractErrorMessage} from "@/utils/errorUtils";
import {ABC_CLASSES, ABC_COLORS, BASIS_LABELS, XYZ_CLASSES, XYZ_COLORS} from "./abcClasses";
import type {useAbcFilters} from "./useAbcFilters";

type Mode = "abc" | "xyz";

interface AbcTimelineDialogProps {
  open: boolean;
  onClose: () => void;
  filters: ReturnType<typeof useAbcFilters>;
}

function AbcTimelineDialog({open, onClose, filters}: AbcTimelineDialogProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const [shown, release] = useRetainedValue(open || null);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="lg"
      fullWidth
      fullScreen={isMobile}
      slotProps={{
        transition: {onExited: release},
        paper: {sx: {pointerEvents: open ? undefined : "none", height: {sm: "85vh"}}},
      }}
    >
      {shown && <AbcTimelineDialogContent filters={filters} onClose={onClose} />}
    </Dialog>
  );
}

const monthFormat = new Intl.DateTimeFormat("ru-RU", {month: "short"});
const yearFormat = new Intl.DateTimeFormat("ru-RU", {month: "long", year: "numeric"});

function cellColors(color: ChipProps["color"]): {bgcolor: string; color: string} {
  return color && color !== "default"
    ? {bgcolor: `${color}.main`, color: `${color}.contrastText`}
    : {bgcolor: "action.selected", color: "text.primary"};
}

function classOf(cell: AbcTimelineCellDto, mode: Mode) {
  return mode === "abc" ? cell.abcClass : cell.xyzClass;
}

function colorOf(cell: AbcTimelineCellDto, mode: Mode): ChipProps["color"] {
  if (mode === "abc") return cell.abcClass ? ABC_COLORS[cell.abcClass] : undefined;
  return cell.xyzClass ? XYZ_COLORS[cell.xyzClass] : undefined;
}

/** First and last class the row had, when they differ; the order of the class list is best to worst. */
function trendOf(cells: AbcTimelineCellDto[], mode: Mode) {
  const order: string[] = mode === "abc" ? ABC_CLASSES : XYZ_CLASSES;
  const classes = cells.map((c) => classOf(c, mode)).filter((c) => c != null);
  const first = classes.at(0);
  const last = classes.at(-1);
  if (!first || !last || first === last) return null;
  return {first, last, up: order.indexOf(last) < order.indexOf(first)};
}

function cellTooltip(month: AbcTimelineMonthDto, cell: AbcTimelineCellDto) {
  const range = `${formatDateOnly(month.from)} – ${formatDateOnly(month.to)}`;
  if (!cell.abcClass) return `${range}: продаж нет`;
  const xyz = cell.xyzClass
    ? `${cell.xyzClass.toUpperCase()}, вариация ${formatPercent(cell.cv)}`
    : "— (мало полных интервалов)";
  return (
    <>
      <Box>{range}</Box>
      <Box>
        ABC: {cell.abcClass.toUpperCase()}, доля {formatPercent(cell.share)}
      </Box>
      <Box>XYZ: {xyz}</Box>
    </>
  );
}

/** Mounted per opening, so the ABC / XYZ switch starts at ABC every time. */
function AbcTimelineDialogContent({
  filters,
  onClose,
}: Pick<AbcTimelineDialogProps, "filters" | "onClose">) {
  const openCatalogItem = useOpenCatalogItem();
  const [mode, setMode] = useState<Mode>("abc");
  const {Page: _page, PageSize: _pageSize, ...query} = filters.query;

  const {data, error, isError, isFetching} = useQuery({
    ...analyticsGetAbcTimelineOptions({query}),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const last = data?.months.at(-1);
  const classFilter = `${filters.abcClass?.toUpperCase() ?? ""}${filters.xyzClass?.toUpperCase() ?? ""}`;
  const subtitle = data && [
    last && `12 месяцев по ${yearFormat.format(parseDateOnly(last.month))}`,
    `окно ${data.windowDays / 7} недель`,
    data.basis === "units"
      ? "по штукам"
      : `${BASIS_LABELS[data.basis].toLowerCase()}${data.currencyCode ? `, ${data.currencyCode}` : ""}`,
    classFilter && `фильтр ${classFilter}`,
    query.SearchString && `поиск «${query.SearchString}»`,
  ];

  return (
    <>
      <DialogTitle sx={{display: "flex", alignItems: "center", gap: 1, pr: 1}}>
        <Box sx={{flexGrow: 1, minWidth: 0}}>
          Классы по месяцам
          <Typography variant="body2" color="text.secondary">
            {subtitle ? subtitle.filter(Boolean).join(" · ") : " "}
          </Typography>
        </Box>
        <IconButton onClick={onClose} aria-label="Закрыть">
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <Box sx={{px: 3, pb: 1.5}}>
        <ToggleButtonGroup
          size="small"
          exclusive
          value={mode}
          onChange={(_, value: Mode | null) => value && setMode(value)}
        >
          <ToggleButton value="abc">ABC</ToggleButton>
          <ToggleButton value="xyz">XYZ</ToggleButton>
        </ToggleButtonGroup>
      </Box>
      <Box sx={{height: 2}}>{isFetching && <LinearProgress sx={{height: 2}} />}</Box>
      <DialogContent dividers sx={{p: 0}}>
        {isError && (
          <Alert severity="error" sx={{m: 2}}>
            {extractErrorMessage(error)}
          </Alert>
        )}
        {data &&
          (data.rows.length === 0 ? (
            <Typography color="text.secondary" sx={{p: 3}}>
              {classFilter || query.SearchString
                ? "Ничего не найдено"
                : "Продаж товаров за период нет"}
            </Typography>
          ) : (
            <Table size="small" stickyHeader sx={{"& td, & th": {px: 0.5, py: 0.5}}}>
              <TableHead>
                <TableRow>
                  <TableCell sx={{pl: "16px !important", minWidth: 200}}>Позиция</TableCell>
                  {data.months.map((month) => (
                    <TableCell key={month.month} align="center" sx={{minWidth: 40}}>
                      <Tooltip
                        title={
                          <>
                            <Box>
                              {formatDateOnly(month.from)} – {formatDateOnly(month.to)}
                              {month.isPartial && " (месяц не полный)"}
                            </Box>
                            <Box>Полных интервалов XYZ: {month.xyzIntervals}</Box>
                          </>
                        }
                      >
                        <span>
                          {monthFormat.format(parseDateOnly(month.month))}
                          {month.isPartial && "*"}
                        </span>
                      </Tooltip>
                    </TableCell>
                  ))}
                  <TableCell align="right" sx={{pr: "16px !important"}}>
                    Тренд
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.rows.map((row) => {
                  const trend = trendOf(row.cells, mode);
                  return (
                    <TableRow key={row.catalogItemId} hover>
                      <TableCell sx={{pl: "16px !important", wordBreak: "break-word"}}>
                        <CatalogItemLink catalogItemId={row.catalogItemId} onOpen={openCatalogItem}>
                          <Typography variant="body2">
                            <Box component="span" sx={{color: "text.secondary", mr: 0.75}}>
                              {row.rank}
                            </Box>
                            {row.name}
                          </Typography>
                        </CatalogItemLink>
                      </TableCell>
                      {row.cells.map((cell, i) => {
                        const value = classOf(cell, mode);
                        const month = data.months[i];
                        return (
                          <TableCell key={month.month}>
                            <Tooltip title={cellTooltip(month, cell)}>
                              <Box
                                sx={[
                                  {
                                    height: 26,
                                    borderRadius: 0.75,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    fontSize: 12,
                                    fontWeight: 600,
                                  },
                                  value
                                    ? cellColors(colorOf(cell, mode))
                                    : cell.abcClass
                                      ? // Sold, but too few full intervals for an XYZ class
                                        {
                                          border: 1,
                                          borderColor: "divider",
                                          borderStyle: "dashed",
                                          color: "text.secondary",
                                        }
                                      : {bgcolor: "action.hover", color: "text.disabled"},
                                ]}
                              >
                                {value ? value.toUpperCase() : cell.abcClass ? "—" : "·"}
                              </Box>
                            </Tooltip>
                          </TableCell>
                        );
                      })}
                      <TableCell align="right" sx={{pr: "16px !important", whiteSpace: "nowrap"}}>
                        {trend ? (
                          <Typography
                            variant="body2"
                            sx={{color: trend.up ? "success.main" : "error.main"}}
                          >
                            {trend.up ? "↗" : "↘"} {trend.first.toUpperCase()}→
                            {trend.last.toUpperCase()}
                          </Typography>
                        ) : (
                          <Typography variant="body2" color="text.disabled">
                            —
                          </Typography>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          ))}
      </DialogContent>
    </>
  );
}

export default AbcTimelineDialog;
