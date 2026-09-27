import {
  IconButton,
  MenuItem,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import DateField from "@/components/DateField";
import {
  isCalendarPreset,
  PERIOD_PRESET_LABELS,
  type Period,
  type PeriodPreset,
  type PeriodSelection,
  periodLabel,
  resolvePeriod,
  shiftPeriod,
  withPreset,
} from "./periodSelection";

interface PeriodPickerProps {
  value: PeriodSelection;
  onChange: (value: PeriodSelection) => void;
  presets: readonly PeriodPreset[];
  pagePeriod: Period;
  /** Toggle buttons for a roomy header, a compact select for a card. */
  variant: "toggles" | "select";
}

function PeriodPicker({value, onChange, presets, pagePeriod, variant}: PeriodPickerProps) {
  const period = resolvePeriod(value, pagePeriod);
  const selectPreset = (preset: PeriodPreset) => onChange(withPreset(preset, period));
  const compact = variant === "select";

  return (
    <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
      {compact ? (
        <TextField
          select
          size="small"
          value={value.preset}
          onChange={(e) => selectPreset(e.target.value as PeriodPreset)}
          sx={{width: 175}}
          slotProps={{htmlInput: {"aria-label": "Период"}}}
        >
          {presets.map((p) => (
            <MenuItem key={p} value={p}>
              {PERIOD_PRESET_LABELS[p]}
            </MenuItem>
          ))}
        </TextField>
      ) : (
        <ToggleButtonGroup
          exclusive
          size="small"
          value={value.preset}
          onChange={(_, preset: PeriodPreset | null) => preset && selectPreset(preset)}
        >
          {presets.map((p) => (
            <ToggleButton key={p} value={p}>
              {PERIOD_PRESET_LABELS[p]}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
      )}

      {value.preset === "custom" ? (
        <>
          <DateField
            size="small"
            label="С"
            value={value.from}
            onChange={(from) => from && onChange({...value, from})}
            sx={{width: compact ? 150 : 165}}
          />
          <DateField
            size="small"
            label="По"
            value={value.to}
            onChange={(to) => to && onChange({...value, to})}
            sx={{width: compact ? 150 : 165}}
          />
        </>
      ) : isCalendarPreset(value.preset) ? (
        <Stack direction="row" sx={{alignItems: "center"}}>
          <IconButton
            size="small"
            onClick={() => onChange(shiftPeriod(value, -1))}
            aria-label="Назад"
          >
            <ChevronLeftIcon />
          </IconButton>
          <Typography
            variant={compact ? "body2" : "body1"}
            sx={{minWidth: compact ? 110 : 150, textAlign: "center"}}
          >
            {periodLabel(value, period)}
          </Typography>
          <IconButton
            size="small"
            onClick={() => onChange(shiftPeriod(value, 1))}
            aria-label="Вперёд"
          >
            <ChevronRightIcon />
          </IconButton>
        </Stack>
      ) : (
        value.preset !== "page" && (
          <Typography variant="body2" color="text.secondary">
            {periodLabel(value, period)}
          </Typography>
        )
      )}
    </Stack>
  );
}

export default PeriodPicker;
