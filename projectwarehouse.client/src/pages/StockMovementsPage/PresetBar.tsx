import {Button, Chip, CircularProgress, Stack, Tooltip, Typography} from "@mui/material";
import CloseFullscreenIcon from "@mui/icons-material/CloseFullscreen";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import StarIcon from "@mui/icons-material/Star";
import TuneIcon from "@mui/icons-material/Tune";
import type {StockMovementReportPresetDto} from "@/api/types.gen";

interface PresetBarProps {
  presets: StockMovementReportPresetDto[];
  activeId: string | undefined;
  isLoading: boolean;
  /** True while the table shows metrics that differ from the saved preset. */
  isDirty: boolean;
  expanded: boolean;
  onSelect: (id: string) => void;
  onEdit: () => void;
  onToggleExpanded: () => void;
}

function PresetBar({
  presets,
  activeId,
  isLoading,
  isDirty,
  expanded,
  onSelect,
  onEdit,
  onToggleExpanded,
}: PresetBarProps) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
      <Typography variant="caption" sx={{color: "text.secondary"}}>
        Пресет
      </Typography>

      {isLoading && <CircularProgress size={16} />}

      {presets.map((preset) => (
        <Chip
          key={preset.id}
          size="small"
          label={preset.name}
          icon={preset.isDefault ? <StarIcon /> : undefined}
          color={preset.id === activeId ? "primary" : "default"}
          variant={preset.id === activeId ? "filled" : "outlined"}
          onClick={() => onSelect(preset.id)}
        />
      ))}

      {isDirty && (
        <Tooltip title="Метрики изменены и ещё не сохранены">
          <Chip size="small" label="черновик" color="warning" variant="outlined" />
        </Tooltip>
      )}

      {/* The editor drawer sits below the full-tab dialog, so the button is only offered outside it. */}
      {!expanded && (
        <Button
          size="small"
          startIcon={<TuneIcon />}
          onClick={onEdit}
          disabled={presets.length === 0}
        >
          Метрики
        </Button>
      )}

      <Button
        size="small"
        sx={{ml: "auto"}}
        startIcon={expanded ? <CloseFullscreenIcon /> : <OpenInFullIcon />}
        onClick={onToggleExpanded}
      >
        {expanded ? "Свернуть" : "На всю вкладку"}
      </Button>
    </Stack>
  );
}

export default PresetBar;
