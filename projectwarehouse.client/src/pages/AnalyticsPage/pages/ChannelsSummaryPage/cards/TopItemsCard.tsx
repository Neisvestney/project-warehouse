import {Alert, Button, Stack, Typography} from "@mui/material";
import {extractErrorMessage} from "@/utils/errorUtils";
import {formatNumber} from "@/components/analytics/analyticsFormat";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {CARD_PERIOD_PRESETS} from "@/components/analytics/period/usePeriodParam";
import SummaryCard from "@/components/analytics/SummaryCard";
import type {useChannelsSummaryFilters} from "../useChannelsSummaryFilters";
import TopItemsControls from "./TopItemsControls";
import TopItemsTable from "./TopItemsTable";
import {TOP_ITEMS_SHOWN, useTopItemsQuery, useTopItemsState} from "./useTopItems";

interface TopItemsCardProps {
  filters: ReturnType<typeof useChannelsSummaryFilters>;
  onShowAll: () => void;
}

function TopItemsCard({filters, onShowAll}: TopItemsCardProps) {
  const state = useTopItemsState(filters);
  const {data, error, isError, isFetching} = useTopItemsQuery(state, TOP_ITEMS_SHOWN);

  return (
    <SummaryCard
      title="Топ товаров"
      isFetching={isFetching}
      actions={
        <PeriodPicker
          variant="compact"
          presets={CARD_PERIOD_PRESETS}
          value={state.selection}
          onChange={state.setSelection}
          pagePeriod={state.pagePeriod}
        />
      }
    >
      <Stack spacing={1.5}>
        <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}>
          <TopItemsControls
            state={state}
            currencies={data?.currencies ?? []}
            currencyCode={data?.currencyCode}
          />
        </Stack>

        {isError && <Alert severity="error">{extractErrorMessage(error)}</Alert>}
        {data &&
          (data.items.length === 0 ? (
            <Typography color="text.secondary">Продаж товаров за период нет</Typography>
          ) : (
            <TopItemsTable
              rows={data.items.map((item, i) => ({rank: i + 1, item}))}
              currencyCode={data.currencyCode}
            />
          ))}

        {data && (data.totalItems > data.items.length || data.unlinkedLines > 0) && (
          <Stack direction="row" useFlexGap sx={{flexWrap: "wrap", gap: 1, alignItems: "center"}}>
            {data.unlinkedLines > 0 && (
              <Typography variant="caption" color="text.secondary">
                Строк без привязки к товару: {formatNumber(data.unlinkedLines)} — в рейтинг не вошли
              </Typography>
            )}
            {data.totalItems > data.items.length && (
              <Button size="small" onClick={onShowAll} sx={{ml: "auto"}}>
                Весь список ({formatNumber(data.totalItems)})
              </Button>
            )}
          </Stack>
        )}
      </Stack>
    </SummaryCard>
  );
}

export default TopItemsCard;
