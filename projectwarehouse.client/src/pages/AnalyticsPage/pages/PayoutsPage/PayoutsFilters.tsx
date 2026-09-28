import {Stack} from "@mui/material";
import ChannelsSelect from "@/components/analytics/ChannelsSelect";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import type {Period} from "@/components/analytics/period/periodSelection";
import FiltersBar from "@/components/FiltersBar";
import {PAYOUTS_PERIOD_PRESETS, type usePayoutsFilters} from "./usePayoutsFilters";

type PayoutsFiltersProps = ReturnType<typeof usePayoutsFilters> & {
  /** The span of the last response, which «За всё время» shows as its dates. */
  reportedPeriod: Period | undefined;
};

function PayoutsFilters({
  selection,
  from,
  to,
  channels,
  setSelection,
  setChannels,
  reportedPeriod,
}: PayoutsFiltersProps) {
  return (
    <Stack spacing={1.5}>
      <Stack
        direction="row"
        useFlexGap
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          "& .MuiToggleButton-root": {height: 40},
        }}
      >
        <PeriodPicker
          variant="toggles"
          presets={PAYOUTS_PERIOD_PRESETS}
          value={selection}
          onChange={setSelection}
          pagePeriod={
            selection.preset === "allTime" && reportedPeriod ? reportedPeriod : {from, to}
          }
        />
      </Stack>

      <FiltersBar activeCount={channels.length > 0 ? 1 : 0}>
        <ChannelsSelect value={channels} onChange={setChannels} withDirect={false} />
      </FiltersBar>
    </Stack>
  );
}

export default PayoutsFilters;
