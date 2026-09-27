import {MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Tooltip} from "@mui/material";
import type {AnalyticsAbcBasis} from "@/api/types.gen";
import ChannelsSelect from "@/components/analytics/ChannelsSelect";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {PERIOD_PRESETS} from "@/components/analytics/period/periodSelection";
import FiltersBar from "@/components/FiltersBar";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import {useHasPermission} from "@/hooks/usePermission";
import {BASIS_LABELS} from "./abcClasses";
import type {useAbcFilters} from "./useAbcFilters";

type AbcFiltersProps = ReturnType<typeof useAbcFilters> & {
  /** Currencies the period met, from the last response; the picker shows only when there is a choice. */
  currencies: string[];
  currencyCode: string | null | undefined;
};

const BASES: AnalyticsAbcBasis[] = ["units", "price", "payout"];

/** Period, basis and currency reshape every class, so they stay in view above the channel filters. */
function AbcFilters({
  selection,
  from,
  to,
  channels,
  directTagIds,
  basis,
  xyzFromFirstSale,
  currencies,
  currencyCode,
  setSelection,
  setChannels,
  setDirectTagIds,
  setBasis,
  setCurrencyCode,
  setXyzFromFirstSale,
}: AbcFiltersProps) {
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
        <Stack direction="row" spacing={1.5} sx={{ml: "auto", alignItems: "center"}}>
          {basis !== "units" && currencies.length > 1 && (
            <TextField
              select
              size="small"
              label="Валюта"
              value={currencyCode ?? ""}
              onChange={(e) => setCurrencyCode(e.target.value)}
              sx={{width: 110}}
            >
              {currencies.map((c) => (
                <MenuItem key={c} value={c}>
                  {c}
                </MenuItem>
              ))}
            </TextField>
          )}
          <ToggleButtonGroup
            exclusive
            size="small"
            value={basis}
            onChange={(_, value: AnalyticsAbcBasis | null) => value && setBasis(value)}
          >
            {BASES.map((b) => (
              <ToggleButton key={b} value={b}>
                {BASIS_LABELS[b]}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
          <Tooltip title="Ряд XYZ позиции начинается с недели её первой продажи: недели до запуска товара — не нулевой спрос">
            <ToggleButton
              size="small"
              value="xyzFromFirstSale"
              selected={xyzFromFirstSale}
              onChange={() => setXyzFromFirstSale(!xyzFromFirstSale)}
            >
              XYZ с первой продажи
            </ToggleButton>
          </Tooltip>
        </Stack>
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

export default AbcFilters;
