import {Alert, Box, LinearProgress, Stack, Typography} from "@mui/material";
import {keepPreviousData, useQuery, useQueryClient} from "@tanstack/react-query";
import {
  analyticsGetChannelsCancellationsQueryKey,
  analyticsGetChannelsLossesQueryKey,
  analyticsGetChannelsReturnsQueryKey,
  analyticsGetChannelsSummaryOptions,
  analyticsGetChannelsSummaryQueryKey,
  analyticsGetChannelsTimeseriesQueryKey,
  analyticsGetChannelsTopItemsQueryKey,
  analyticsGetChannelsWeekdaysQueryKey,
} from "@/api/@tanstack/react-query.gen";
import AnalyticsPageHeader from "@/components/analytics/AnalyticsPageHeader";
import CatalogItemDrawerHost from "@/components/catalog/CatalogItemDrawerHost";
import QueryError from "@/components/QueryError";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {formatDateOnly} from "@/utils/dateOnly";
import {extractErrorMessage} from "@/utils/errorUtils";
import CancellationsCard from "./cards/CancellationsCard";
import DynamicsCard from "./cards/DynamicsCard";
import LossesCard from "./cards/LossesCard";
import ReturnsCard from "./cards/ReturnsCard";
import SharesCard from "./cards/SharesCard";
import TopItemsCard from "./cards/TopItemsCard";
import TopItemsDialog from "./cards/TopItemsDialog";
import WeekdaysCard from "./cards/WeekdaysCard";
import {DIRECT_CHANNEL} from "@/components/analytics/channelsQuery";
import ChannelsSummaryFilters from "./ChannelsSummaryFilters";
import CancellationsFullscreen from "./fullscreen/CancellationsFullscreen";
import DynamicsFullscreen from "./fullscreen/DynamicsFullscreen";
import ReturnsFullscreen from "./fullscreen/ReturnsFullscreen";
import {useChartFullscreen} from "./fullscreen/useChartFullscreen";
import ChannelsSummaryTable from "./ChannelsSummaryTable";
import {useChannelsSummaryFilters} from "./useChannelsSummaryFilters";

function ChannelsSummaryPage() {
  const filters = useChannelsSummaryFilters();

  const fullscreen = useChartFullscreen();
  const [topList, openTopList, closeTopList] = useDrawerSearchParamsState("toplist");
  const queryClient = useQueryClient();

  const {data, error, isError, isFetching, isPlaceholderData} = useQuery({
    ...analyticsGetChannelsSummaryOptions({query: filters.query}),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const appliedSettings = data && [
    `Сутки считаются по часовому поясу ${data.timeZoneId}`,
    `Δ — к периоду ${formatDateOnly(data.previousFrom)} — ${formatDateOnly(data.previousTo)}`,
    `Возвраты созревают ${data.returnsMaturityDays} дн.`,
  ];

  const refreshAll = () =>
    Promise.all(
      [
        analyticsGetChannelsSummaryQueryKey(),
        analyticsGetChannelsTimeseriesQueryKey(),
        analyticsGetChannelsReturnsQueryKey(),
        analyticsGetChannelsCancellationsQueryKey(),
        analyticsGetChannelsLossesQueryKey(),
        analyticsGetChannelsTopItemsQueryKey(),
        analyticsGetChannelsWeekdaysQueryKey(),
      ].map((queryKey) => queryClient.invalidateQueries({queryKey})),
    );

  return (
    <CatalogItemDrawerHost>
      <Stack spacing={2}>
        <AnalyticsPageHeader
          title="Сводка по каналам"
          appliedSettings={appliedSettings}
          onRefresh={refreshAll}
          settingsGroup="channels"
        />

        <ChannelsSummaryFilters {...filters} />

        <Box sx={{height: 4}}>{isFetching && <LinearProgress />}</Box>

        {isError && data && isPlaceholderData && (
          // A rejected filter (say, an inverted period) must not hide behind the previous result
          <Alert severity="error">{extractErrorMessage(error)}</Alert>
        )}

        {isError && !data ? (
          <QueryError error={error} />
        ) : (
          data && (
            <Stack spacing={1}>
              <ChannelsSummaryTable summary={data} />
              {data.rows.some((r) => r.kind === "directTag") && (
                <Typography variant="caption" color="text.secondary">
                  Заказ с несколькими тегами входит в строку каждого из них, поэтому строки тегов не
                  складываются в «Прямые · все» — она считается по самим заказам.
                </Typography>
              )}
            </Stack>
          )
        )}

        <DynamicsCard
          filters={filters}
          onExpand={() =>
            fullscreen.openWith("dynamics", {
              channels: filters.channels,
              selection: filters.selection,
              step: filters.step,
              measure: filters.measure,
            })
          }
        />

        <LossesCard filters={filters} />

        <SharesCard filters={filters} />

        {/* Paired cards of a row stretch to the taller one, so each row ends level */}
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {xs: "minmax(0, 1fr)", md: "repeat(2, minmax(0, 1fr))"},
            gap: 2,
          }}
        >
          <CancellationsCard
            filters={filters}
            onExpand={(selection, step, share) =>
              fullscreen.openWith("cancellations", {
                // Only shops have the breakdown; «Прямые» alone leaves an empty pick, which reads as every shop
                channels: filters.channels.filter((c) => c !== DIRECT_CHANNEL),
                selection,
                step,
                share,
              })
            }
          />
          <ReturnsCard
            filters={filters}
            onExpand={(selection, step) =>
              fullscreen.openWith("returns", {
                // Only shops have returns; «Прямые» alone leaves an empty pick, which reads as every shop
                channels: filters.channels.filter((c) => c !== DIRECT_CHANNEL),
                selection,
                step,
              })
            }
          />
          <TopItemsCard filters={filters} onShowAll={() => openTopList("all")} />
          <WeekdaysCard filters={filters} />
        </Box>

        <TopItemsDialog open={!!topList} onClose={closeTopList} filters={filters} />
        <DynamicsFullscreen state={fullscreen} directTagIds={filters.directTagIds} />
        <ReturnsFullscreen state={fullscreen} moneyMode={filters.moneyMode} />
        <CancellationsFullscreen state={fullscreen} />
      </Stack>
    </CatalogItemDrawerHost>
  );
}

export default ChannelsSummaryPage;
