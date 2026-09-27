import {useState} from "react";
import {Alert, Box, Button, IconButton, Stack, Tooltip, Typography} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import RefreshIcon from "@mui/icons-material/Refresh";
import SettingsIcon from "@mui/icons-material/Settings";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetAbcOptions} from "@/api/@tanstack/react-query.gen";
import {formatPercent} from "@/components/analytics/analyticsFormat";
import AnalyticsSettingsDialog from "@/components/analytics/AnalyticsSettingsDialog";
import SummaryCard from "@/components/analytics/SummaryCard";
import CatalogItemDrawerHost from "@/components/catalog/CatalogItemDrawerHost";
import LoadingOverlay from "@/components/LoadingOverlay";
import PageGenericHeader from "@/components/PageGenericHeader";
import PageLoader from "@/components/PageLoader";
import QueryError from "@/components/QueryError";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {useHasPermission} from "@/hooks/usePermission";
import {extractErrorMessage} from "@/utils/errorUtils";
import AbcClassTiles from "./AbcClassTiles";
import AbcFilters from "./AbcFilters";
import AbcItemsTable from "./AbcItemsTable";
import AbcMatrix from "./AbcMatrix";
import AbcTimelineDialog from "./AbcTimelineDialog";
import {formatBoundary} from "@/components/analytics/abc/abcClasses";
import ParetoChart from "./ParetoChart";
import {useAbcFilters} from "./useAbcFilters";

function AbcPage() {
  const filters = useAbcFilters();
  const canEditSettings = useHasPermission("analytics.settings");
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [timeline, openTimeline, closeTimeline] = useDrawerSearchParamsState("abctimeline");

  const {data, error, isError, isFetching, isLoading, isPlaceholderData, refetch} = useQuery({
    ...analyticsGetAbcOptions({query: filters.query}),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  // Paging, search and the class filter only narrow the table; everything else recomputes the report
  const {
    Page: _page,
    PageSize: _pageSize,
    SearchString: _search,
    AbcClass: _abc,
    XyzClass: _xyz,
    ...reportQuery
  } = filters.query;
  const reportKey = JSON.stringify(reportQuery);
  const [shownReportKey, setShownReportKey] = useState(reportKey);
  if (data && !isPlaceholderData && shownReportKey !== reportKey) setShownReportKey(reportKey);
  // A placeholder of the same report means only the table is on its way; a refresh refetches it all
  const tableOnlyFetching = isFetching && isPlaceholderData && shownReportKey === reportKey;

  const appliedSettings = data && [
    `Сутки считаются по часовому поясу ${data.timeZoneId}`,
    `Границы ABC: ${formatBoundary(data.settings.abcBoundaryA)} и ${formatBoundary(data.settings.abcBoundaryB)}`,
    `Границы XYZ: ${formatBoundary(data.settings.xyzBoundaryX)} и ${formatBoundary(data.settings.xyzBoundaryY)}, шаг — ${data.settings.xyzStep === "week" ? "неделя" : "месяц"}`,
  ];

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <PageGenericHeader
          title={
            // Applied parameters hang off a permanently rendered icon so the header does not jump on reload
            <>
              ABC / XYZ
              <Tooltip
                title={
                  appliedSettings && appliedSettings.map((line) => <Box key={line}>{line}</Box>)
                }
              >
                <InfoOutlinedIcon
                  sx={{
                    fontSize: "0.7em",
                    verticalAlign: "middle",
                    ml: 0.5,
                    color: "primary.main",
                    opacity: appliedSettings ? 1 : 0,
                    pointerEvents: appliedSettings ? "auto" : "none",
                  }}
                />
              </Tooltip>
            </>
          }
          refresh={
            <Tooltip title="Обновить">
              <IconButton color="inherit" onClick={() => refetch()}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          }
          actions={
            canEditSettings && (
              <Button
                variant="outlined"
                startIcon={<SettingsIcon />}
                onClick={() => setSettingsOpen(true)}
              >
                Настройки
              </Button>
            )
          }
        />

        <AbcFilters
          {...filters}
          currencies={data?.currencies ?? []}
          currencyCode={data?.currencyCode}
        />

        {isError && data && isPlaceholderData && (
          // A rejected filter (say, an inverted period) must not hide behind the previous result
          <Alert severity="error">{extractErrorMessage(error)}</Alert>
        )}

        {isLoading ? (
          <PageLoader inline />
        ) : isError && !data ? (
          <QueryError error={error} />
        ) : (
          data && (
            // One overlay over the whole report instead of a bar per card; the filters above stay usable
            <Box sx={{position: "relative"}}>
              <LoadingOverlay open={isFetching && !tableOnlyFetching} alignTop />
              <Stack spacing={2}>
                {data.basis !== "units" && !data.currencyCode && (
                  <Alert severity="info">
                    У выбранных магазинов нет продаж с ценой за период — денежная база пуста.
                    «Прямые» в ней не участвуют: у них нет денег.
                  </Alert>
                )}
                {data.basis === "payout" && data.payoutCoverage != null && (
                  <Alert severity="warning">
                    Начислено {formatPercent(data.payoutCoverage)} строк-продаж периода: выплата по
                    отправлениям в пути ещё не пришла, и то, что продаётся прямо сейчас,
                    недооценено.
                  </Alert>
                )}

                <AbcClassTiles
                  data={data}
                  selected={filters.abcClass}
                  onSelect={(abc) => filters.setMatrixCell(abc, null)}
                />

                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: {xs: "minmax(0, 1fr)", md: "3fr 2fr"},
                    gap: 2,
                  }}
                >
                  <SummaryCard title="Парето">
                    {data.pareto.length === 0 ? (
                      <Typography color="text.secondary">Продаж товаров за период нет</Typography>
                    ) : (
                      <ParetoChart data={data} height={260} />
                    )}
                  </SummaryCard>
                  <SummaryCard title="Матрица ABC × XYZ" subtitle="клик фильтрует таблицу">
                    <AbcMatrix
                      data={data}
                      abcClass={filters.abcClass}
                      xyzClass={filters.xyzClass}
                      onSelect={filters.setMatrixCell}
                    />
                  </SummaryCard>
                </Box>

                <AbcItemsTable
                  data={data}
                  isFetching={tableOnlyFetching}
                  abcClass={filters.abcClass}
                  xyzClass={filters.xyzClass}
                  onClearClass={() => filters.setMatrixCell(null, null)}
                  search={filters.searchInput}
                  onSearchChange={filters.setSearchInput}
                  page={filters.page}
                  pageSize={filters.pageSize}
                  onPageChange={filters.setPage}
                  onPageSizeChange={filters.setPageSize}
                  onShowTimeline={() => openTimeline("open")}
                />
              </Stack>
            </Box>
          )
        )}

        <AbcTimelineDialog open={!!timeline} onClose={closeTimeline} filters={filters} />
        <AnalyticsSettingsDialog
          open={settingsOpen}
          group="abc"
          onClose={() => setSettingsOpen(false)}
        />
      </Stack>
    </CatalogItemDrawerHost>
  );
}

export default AbcPage;
