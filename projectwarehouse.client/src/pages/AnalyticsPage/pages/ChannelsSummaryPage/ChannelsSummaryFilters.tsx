import {Stack, ToggleButton, ToggleButtonGroup} from "@mui/material";
import type {AnalyticsMoneyMode} from "@/api/types.gen";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import FiltersBar from "@/components/FiltersBar";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import {useCatalogItemsByIds} from "@/hooks/useCatalogItemsByIds";
import {useHasPermission} from "@/hooks/usePermission";
import ChannelsSelect from "@/components/analytics/ChannelsSelect";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {PERIOD_PRESETS} from "@/components/analytics/period/periodSelection";
import type {useChannelsSummaryFilters} from "./useChannelsSummaryFilters";

type ChannelsSummaryFiltersProps = ReturnType<typeof useChannelsSummaryFilters>;

/**
 * The period and the money mode shape every number on the page, so they sit in a row of their own above the
 * bar, always in view; the bar keeps the filters that narrow the channels.
 */
function ChannelsSummaryFilters({
  selection,
  from,
  to,
  channels,
  directTagIds,
  catalogItemIds,
  moneyMode,
  setSelection,
  setChannels,
  setDirectTagIds,
  setCatalogItemIds,
  setMoneyMode,
}: ChannelsSummaryFiltersProps) {
  // The tag list is served by the orders module; without its right the Direct channel stays unfiltered
  const canViewOrderTags = useHasPermission(["orders.view", "orders.view_assigned"]);

  const {items: knownItems} = useCatalogItemsByIds(catalogItemIds);
  // A placeholder keeps an unresolved id selected, so an edit made before the names load does not drop it
  const items = catalogItemIds.map(
    (id) =>
      knownItems.get(id) ?? {
        id,
        type: "standard" as const,
        name: "…",
        fullName: "…",
        article: "",
        isArchived: false,
      },
  );

  return (
    <Stack spacing={1.5}>
      <Stack
        direction="row"
        useFlexGap
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          // Level with the 40px date fields of a custom period
          "& .MuiToggleButton-root": {height: 40},
        }}
      >
        <PeriodPicker
          variant="toggles"
          presets={PERIOD_PRESETS}
          value={selection}
          onChange={setSelection}
          pagePeriod={{from, to}}
        />
        <ToggleButtonGroup
          exclusive
          size="small"
          value={moneyMode}
          onChange={(_, value: AnalyticsMoneyMode | null) => value && setMoneyMode(value)}
          sx={{ml: "auto"}}
        >
          <ToggleButton value="price">Цена продажи</ToggleButton>
          <ToggleButton
            value="payout"
            title="Нетто журнала начислений: продажа за вычетом комиссии, логистики, эквайринга и сторно — по начисленным заказам периода"
          >
            Выплата
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      <FiltersBar
        activeCount={
          [channels.length > 0, directTagIds.length > 0, catalogItemIds.length > 0].filter(Boolean)
            .length
        }
      >
        <ChannelsSelect value={channels} onChange={setChannels} />

        <CatalogItemsSelect
          multiple
          size="small"
          label="Товары"
          value={items}
          onChange={(value) => setCatalogItemIds(value.map((item) => item.id))}
          sx={{minWidth: 280, flexGrow: 1}}
        />

        {canViewOrderTags && (
          <DocumentTagsFilter
            kind="order"
            label="Теги прямых заказов"
            value={directTagIds}
            onChange={setDirectTagIds}
            sx={{width: 240}}
          />
        )}
      </FiltersBar>
    </Stack>
  );
}

export default ChannelsSummaryFilters;
