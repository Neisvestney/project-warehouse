import {Alert, Box, Stack} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetPayoutsOptions} from "@/api/@tanstack/react-query.gen";
import AnalyticsPageHeader from "@/components/analytics/AnalyticsPageHeader";
import LoadingOverlay from "@/components/LoadingOverlay";
import PageLoader from "@/components/PageLoader";
import QueryError from "@/components/QueryError";
import {useDrawerSearchParamsState} from "@/hooks/useDrawerSearchParamsState";
import {extractErrorMessage} from "@/utils/errorUtils";
import AccrualsDynamicsCard from "./AccrualsDynamicsCard";
import AccrualsFullscreen from "./AccrualsFullscreen";
import PayoutsCategoriesCard from "./PayoutsCategoriesCard";
import PayoutsFilters from "./PayoutsFilters";
import PayoutsPostingsDialog from "./PayoutsPostingsDialog";
import {
  decodePostingsTarget,
  encodePostingsTarget,
  type PostingsTarget,
} from "./payoutsPostingsTarget";
import PayoutsShops from "./PayoutsShops";
import PayoutsTiles from "./PayoutsTiles";
import {useAccrualsFullscreen} from "./useAccrualsFullscreen";
import {usePayoutsFilters} from "./usePayoutsFilters";
import WithholdingsCard from "./WithholdingsCard";

function PayoutsPage() {
  const filters = usePayoutsFilters();
  const fullscreen = useAccrualsFullscreen();
  const [postingsParam, openPostingsParam, closePostings] =
    useDrawerSearchParamsState("payoutPostings");
  const openPostings = (target: PostingsTarget) => openPostingsParam(encodePostingsTarget(target));

  const {data, error, isError, isFetching, isLoading, isPlaceholderData, refetch} = useQuery({
    ...analyticsGetPayoutsOptions({query: filters.query}),
    placeholderData: keepPreviousData,
    meta: {suppressGlobalError: true},
  });

  const appliedSettings = data && [
    "Период — только для «Начислено» и статей начислений; долг площадки считается на сегодня",
    `Сутки считаются по часовому поясу ${data.timeZoneId}`,
    `Доля выплаты — нетто журнала начислений к цене продажи за ${data.settings.payoutRatioWindowDays} дн., без обратной логистики и сторно`,
    `Возраст в пути: границы ${data.settings.payoutAgeBoundaries.join(", ")} дн., застряло — от ${data.settings.payoutOverdueDays} дн.`,
    `«Площадка не начислила» — доставлено ${data.settings.payoutNotAccruedDays}+ дн. назад`,
  ];

  return (
    <Stack spacing={2}>
      <AnalyticsPageHeader
        title="Выплаты маркетплейсов"
        appliedSettings={appliedSettings}
        onRefresh={() => refetch()}
        settingsGroup="payouts"
      />

      <PayoutsFilters {...filters} reportedPeriod={data && {from: data.from, to: data.to}} />

      {isError && data && isPlaceholderData && (
        <Alert severity="error">{extractErrorMessage(error)}</Alert>
      )}

      {isLoading ? (
        <PageLoader inline />
      ) : isError && !data ? (
        <QueryError error={error} />
      ) : (
        data && (
          <Box sx={{position: "relative"}}>
            <LoadingOverlay open={isFetching} alignTop />
            <Stack spacing={2}>
              <PayoutsTiles data={data} onOpenPostings={openPostings} />
              <AccrualsDynamicsCard
                filters={filters}
                onExpand={() =>
                  fullscreen.openWith({
                    channels: filters.channels,
                    selection: filters.selection,
                    step: filters.step,
                    total: filters.showTotal,
                  })
                }
              />
              <WithholdingsCard filters={filters} />
              <PayoutsCategoriesCard data={data} />
              <PayoutsShops data={data} onOpenPostings={openPostings} />
            </Stack>
          </Box>
        )
      )}

      <AccrualsFullscreen state={fullscreen} />
      {data && (
        <PayoutsPostingsDialog
          target={decodePostingsTarget(postingsParam)}
          onClose={closePostings}
          data={data}
          channelsQuery={filters.query}
        />
      )}
    </Stack>
  );
}

export default PayoutsPage;
