import {Stack, ToggleButton, ToggleButtonGroup} from "@mui/material";
import type {AnalyticsMoneyMode} from "@/api/types.gen";
import FiltersBar from "@/components/FiltersBar";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import {useHasPermission} from "@/hooks/usePermission";
import ChannelsSelect from "./ChannelsSelect";
import PeriodPicker from "./period/PeriodPicker";
import {PERIOD_PRESETS} from "./period/periodSelection";
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
  moneyMode,
  setSelection,
  setChannels,
  setDirectTagIds,
  setMoneyMode,
}: ChannelsSummaryFiltersProps) {
  // The tag list is served by the orders module; without its right the Direct channel stays unfiltered
  const canViewOrderTags = useHasPermission(["orders.view", "orders.view_assigned"]);

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
          <ToggleButton value="payout">Выплата</ToggleButton>
        </ToggleButtonGroup>
      </Stack>

      <FiltersBar
        activeCount={[channels.length > 0, directTagIds.length > 0].filter(Boolean).length}
      >
        <ChannelsSelect value={channels} onChange={setChannels} />

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
