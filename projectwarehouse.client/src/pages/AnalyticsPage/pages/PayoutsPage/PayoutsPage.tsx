import {Alert, Box, Stack} from "@mui/material";
import {keepPreviousData, useQuery} from "@tanstack/react-query";
import {analyticsGetPayoutsOptions} from "@/api/@tanstack/react-query.gen";
import AnalyticsPageHeader from "@/components/analytics/AnalyticsPageHeader";
import LoadingOverlay from "@/components/LoadingOverlay";
import PageLoader from "@/components/PageLoader";
import QueryError from "@/components/QueryError";
import {extractErrorMessage} from "@/utils/errorUtils";
import PayoutsCategoriesCard from "./PayoutsCategoriesCard";
import PayoutsFilters from "./PayoutsFilters";
import PayoutsShops from "./PayoutsShops";
import PayoutsTiles from "./PayoutsTiles";
import {usePayoutsFilters} from "./usePayoutsFilters";

function PayoutsPage() {
  const filters = usePayoutsFilters();

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
              <PayoutsTiles data={data} />
              <PayoutsCategoriesCard data={data} />
              <PayoutsShops data={data} />
            </Stack>
          </Box>
        )
      )}
    </Stack>
  );
}

export default PayoutsPage;
