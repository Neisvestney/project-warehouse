import {useState} from "react";
import {
  Button,
  IconButton,
  MenuItem,
  MenuList,
  Popover,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
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
  /** Toggle buttons for a roomy header; for a card header, one button whose popover holds the presets and dates. */
  variant: "toggles" | "compact";
}

function PeriodPicker({variant, ...props}: PeriodPickerProps) {
  return variant === "compact" ? (
    <CompactPeriodPicker {...props} />
  ) : (
    <TogglesPeriodPicker {...props} />
  );
}

type VariantProps = Omit<PeriodPickerProps, "variant">;

function TogglesPeriodPicker({value, onChange, presets, pagePeriod}: VariantProps) {
  const period = resolvePeriod(value, pagePeriod);

  return (
    <Stack direction="row" useFlexGap sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={value.preset}
        onChange={(_, preset: PeriodPreset | null) =>
          preset && onChange(withPreset(preset, period))
        }
      >
        {presets.map((p) => (
          <ToggleButton key={p} value={p}>
            {PERIOD_PRESET_LABELS[p]}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>

      {value.preset === "custom" ? (
        <>
          <DateField
            size="small"
            label="С"
            value={value.from}
            onChange={(from) => from && onChange({...value, from})}
            sx={{width: 165}}
          />
          <DateField
            size="small"
            label="По"
            value={value.to}
            onChange={(to) => to && onChange({...value, to})}
            sx={{width: 165}}
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
          <Typography sx={{minWidth: 150, textAlign: "center"}}>
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

function CompactPeriodPicker({value, onChange, presets, pagePeriod}: VariantProps) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const period = resolvePeriod(value, pagePeriod);
  const calendar = isCalendarPreset(value.preset);
  // Presets named by their rule show the dates they resolve to on hover
  const byRule =
    value.preset === "page" ||
    value.preset === "lastMonth" ||
    value.preset === "lastYear" ||
    value.preset === "allTime";
  const range = periodLabel({preset: "custom", ...period}, period);
  const label = byRule ? PERIOD_PRESET_LABELS[value.preset] : periodLabel(value, period);

  return (
    <>
      <Stack direction="row" sx={{alignItems: "center", flexShrink: 0}}>
        {calendar && (
          <IconButton
            size="small"
            onClick={() => onChange(shiftPeriod(value, -1))}
            aria-label="Назад"
          >
            <ChevronLeftIcon />
          </IconButton>
        )}
        <Tooltip title={byRule && !anchor ? range : ""}>
          <Button
            size="small"
            color="inherit"
            endIcon={<ArrowDropDownIcon />}
            onClick={(e) => setAnchor(e.currentTarget)}
            aria-haspopup="true"
            aria-expanded={!!anchor}
            aria-label={`Период: ${byRule ? `${label}, ${range}` : label}`}
            sx={{textTransform: "none", whiteSpace: "nowrap", fontWeight: 400}}
          >
            {label}
          </Button>
        </Tooltip>
        {calendar && (
          <IconButton
            size="small"
            onClick={() => onChange(shiftPeriod(value, 1))}
            aria-label="Вперёд"
          >
            <ChevronRightIcon />
          </IconButton>
        )}
      </Stack>

      <Popover
        open={!!anchor}
        anchorEl={anchor}
        onClose={() => setAnchor(null)}
        anchorOrigin={{vertical: "bottom", horizontal: "right"}}
        transformOrigin={{vertical: "top", horizontal: "right"}}
      >
        <MenuList dense autoFocusItem variant="selectedMenu">
          {presets.map((p) => (
            <MenuItem
              key={p}
              selected={value.preset === p}
              onClick={() => {
                onChange(withPreset(p, period));
                // Custom dates are edited right here, so that choice keeps the popover open
                if (p !== "custom") setAnchor(null);
              }}
            >
              {PERIOD_PRESET_LABELS[p]}
            </MenuItem>
          ))}
        </MenuList>
        {value.preset === "custom" && (
          <Stack direction="row" spacing={1} sx={{px: 1.5, pb: 1.5, pt: 0.5}}>
            <DateField
              size="small"
              label="С"
              value={value.from}
              onChange={(from) => from && onChange({...value, from})}
              sx={{width: 150}}
            />
            <DateField
              size="small"
              label="По"
              value={value.to}
              onChange={(to) => to && onChange({...value, to})}
              sx={{width: 150}}
            />
          </Stack>
        )}
      </Popover>
    </>
  );
}

export default PeriodPicker;
