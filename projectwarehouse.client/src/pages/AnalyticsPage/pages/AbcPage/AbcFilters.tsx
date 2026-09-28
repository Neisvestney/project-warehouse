import {MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Tooltip} from "@mui/material";
import type {AnalyticsAbcBasis, AnalyticsAbcSubject} from "@/api/types.gen";
import ChannelsSelect from "@/components/analytics/ChannelsSelect";
import PeriodPicker from "@/components/analytics/period/PeriodPicker";
import {PERIOD_PRESETS} from "@/components/analytics/period/periodSelection";
import FiltersBar from "@/components/FiltersBar";
import DocumentTagsFilter from "@/components/tags/DocumentTagsFilter";
import {useHasPermission} from "@/hooks/usePermission";
import {BASIS_LABELS} from "@/components/analytics/abc/abcClasses";
import {SUBJECT_LABELS, SUBJECT_TOOLTIPS, SUBJECTS} from "./abcSubjects";
import type {useAbcFilters} from "./useAbcFilters";

type AbcFiltersProps = ReturnType<typeof useAbcFilters> & {
  /** Currencies the period met, from the last response; the picker shows only when there is a choice. */
  currencies: string[];
  currencyCode: string | null | undefined;
};

const BASES: AnalyticsAbcBasis[] = ["units", "price", "payout"];

/**
 * Same layout as the channels summary: the period on the left and what is counted (basis, currency) on the right of
 * the first row; what a row is and the XYZ series start share the second, the XYZ toggle pinned to the right edge.
 */
function AbcFilters({
  selection,
  from,
  to,
  channels,
  directTagIds,
  basis,
  subject,
  xyzFromFirstSale,
  currencies,
  currencyCode,
  setSelection,
  setChannels,
  setDirectTagIds,
  setBasis,
  setSubject,
  setCurrencyCode,
  setXyzFromFirstSale,
}: AbcFiltersProps) {
  const canViewOrderTags = useHasPermission(["orders.view", "orders.view_assigned"]);
  // Cards and articles leave Direct orders out, so their tags would filter nothing
  const showDirectTags = canViewOrderTags && subject === "catalogItem";

  return (
    <Stack spacing={1.5} sx={{"& .MuiToggleButton-root": {height: 40}}}>
      <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1.5}}>
        <PeriodPicker
          variant="toggles"
          presets={PERIOD_PRESETS}
          value={selection}
          onChange={setSelection}
          pagePeriod={{from, to}}
        />
        <Stack direction="row" useFlexGap sx={{alignItems: "center", gap: 1.5, ml: "auto"}}>
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
        </Stack>
      </Stack>
      <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1.5}}>
        <ToggleButtonGroup
          exclusive
          size="small"
          value={subject}
          onChange={(_, value: AnalyticsAbcSubject | null) => value && setSubject(value)}
        >
          {SUBJECTS.map((s) => (
            <Tooltip key={s} title={SUBJECT_TOOLTIPS[s]}>
              <ToggleButton value={s}>{SUBJECT_LABELS[s].toggle}</ToggleButton>
            </Tooltip>
          ))}
        </ToggleButtonGroup>
        <Tooltip title="Ряд XYZ позиции начинается с недели её первой продажи: недели до запуска товара — не нулевой спрос">
          <ToggleButton
            size="small"
            value="xyzFromFirstSale"
            selected={xyzFromFirstSale}
            onChange={() => setXyzFromFirstSale(!xyzFromFirstSale)}
            sx={{ml: "auto"}}
          >
            XYZ с первой продажи
          </ToggleButton>
        </Tooltip>
      </Stack>

      <FiltersBar
        activeCount={
          [channels.length > 0, showDirectTags && directTagIds.length > 0].filter(Boolean).length
        }
      >
        <ChannelsSelect value={channels} onChange={setChannels} />
        {showDirectTags && (
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
