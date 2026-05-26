import {useState} from "react";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogTitle from "@mui/material/DialogTitle";
import Divider from "@mui/material/Divider";
import IconButton from "@mui/material/IconButton";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Tooltip from "@mui/material/Tooltip";
import DeleteIcon from "@mui/icons-material/Delete";
import PrintIcon from "@mui/icons-material/Print";
import SaveIcon from "@mui/icons-material/Save";
import {
  SYSTEM_PRESETS,
  type PrintPreset,
  type PrintSettings as PrintSettingsType,
  saveCustomPresets,
  saveLastPresetId,
} from "./printPresets.ts";

interface PrintSettingsProps {
  presets: PrintPreset[];
  selectedPresetId: string;
  settings: PrintSettingsType;
  onPresetSelect: (id: string) => void;
  onSettingsChange: (s: PrintSettingsType) => void;
  onCustomPresetsChange: (presets: PrintPreset[]) => void;
  customPresets: PrintPreset[];
}

function NumField({
  label,
  value,
  min,
  onChange,
}: {
  label: string;
  value: number;
  min: number;
  onChange: (v: number) => void;
}) {
  const [raw, setRaw] = useState(String(value));
  const [syncedValue, setSyncedValue] = useState(value);
  if (syncedValue !== value) {
    setSyncedValue(value);
    setRaw(String(value));
  }

  return (
    <TextField
      label={label}
      type="number"
      size="small"
      value={raw}
      onChange={(e) => {
        const s = e.target.value;
        setRaw(s);
        const n = Number(s);
        if (s !== "" && !isNaN(n) && n >= min) onChange(n);
      }}
      onBlur={() => {
        const n = Math.max(min, Number(raw) || 0);
        setRaw(String(n));
        onChange(n);
      }}
      slotProps={{htmlInput: {min, step: 1}}}
      sx={{width: 110}}
    />
  );
}

function PrintSettings({
  presets,
  selectedPresetId,
  settings,
  onPresetSelect,
  onSettingsChange,
  onCustomPresetsChange,
  customPresets,
}: PrintSettingsProps) {
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [newPresetName, setNewPresetName] = useState("");

  const handlePresetChange = (id: string) => {
    onPresetSelect(id);
    saveLastPresetId(id);
  };

  const handleSavePreset = () => {
    const name = newPresetName.trim();
    if (!name) return;
    const newPreset: PrintPreset = {
      id: `custom-${Date.now()}`,
      name,
      settings: {...settings},
      isCustom: true,
    };
    const updated = [...customPresets, newPreset];
    onCustomPresetsChange(updated);
    saveCustomPresets(updated);
    onPresetSelect(newPreset.id);
    saveLastPresetId(newPreset.id);
    setNewPresetName("");
    setSaveDialogOpen(false);
  };

  const handleDeletePreset = (id: string) => {
    const updated = customPresets.filter((p) => p.id !== id);
    onCustomPresetsChange(updated);
    saveCustomPresets(updated);
    if (selectedPresetId === id && SYSTEM_PRESETS[0]) {
      handlePresetChange(SYSTEM_PRESETS[0].id);
    }
  };

  return (
    <Box
      className="no-print"
      sx={{
        p: 2,
        borderBottom: "1px solid",
        borderColor: "divider",
        bgcolor: "background.paper",
        displayPrint: "none",
        "@media print": {display: "none"},
      }}
    >
      <Stack direction="row" spacing={2} sx={{alignItems: "center", flexWrap: "wrap", gap: 1}}>
        <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
          <TextField
            select
            label="Пресет"
            size="small"
            value={selectedPresetId}
            onChange={(e) => handlePresetChange(e.target.value)}
            sx={{minWidth: 160}}
          >
            {SYSTEM_PRESETS.map((p) => (
              <MenuItem key={p.id} value={p.id}>
                {p.name}
              </MenuItem>
            ))}
            {customPresets.length > 0 && <Divider />}
            {customPresets.map((p) => (
              <MenuItem key={p.id} value={p.id}>
                {p.name}
              </MenuItem>
            ))}
          </TextField>
          {customPresets.some((p) => p.id === selectedPresetId) && (
            <Tooltip title="Удалить пресет">
              <IconButton size="small" onClick={() => handleDeletePreset(selectedPresetId)}>
                <DeleteIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          )}
        </Stack>

        <NumField
          label={fieldLabel("labelWidthMm")}
          value={settings.labelWidthMm}
          min={1}
          onChange={(v) => onSettingsChange({...settings, labelWidthMm: v})}
        />
        <NumField
          label={fieldLabel("labelHeightMm")}
          value={settings.labelHeightMm}
          min={1}
          onChange={(v) => onSettingsChange({...settings, labelHeightMm: v})}
        />
        <NumField
          label={fieldLabel("columns")}
          value={settings.columns}
          min={1}
          onChange={(v) => onSettingsChange({...settings, columns: v})}
        />
        <NumField
          label={fieldLabel("gapMm")}
          value={settings.gapMm}
          min={0}
          onChange={(v) => onSettingsChange({...settings, gapMm: v})}
        />
        <NumField
          label={fieldLabel("pagePaddingMm")}
          value={settings.pagePaddingMm}
          min={0}
          onChange={(v) => onSettingsChange({...settings, pagePaddingMm: v})}
        />
        <NumField
          label={fieldLabel("labelPaddingMm")}
          value={settings.labelPaddingMm}
          min={0}
          onChange={(v) => onSettingsChange({...settings, labelPaddingMm: v})}
        />
        <NumField
          label={fieldLabel("fontSizePx")}
          value={settings.fontSizePx}
          min={1}
          onChange={(v) => onSettingsChange({...settings, fontSizePx: v})}
        />

        <Tooltip title="Сохранить текущие настройки как пресет">
          <Button
            variant="outlined"
            size="small"
            startIcon={<SaveIcon />}
            onClick={() => setSaveDialogOpen(true)}
          >
            Сохранить
          </Button>
        </Tooltip>

        <Button
          variant="contained"
          size="small"
          startIcon={<PrintIcon />}
          onClick={() => window.print()}
          sx={{ml: "auto"}}
        >
          Печать
        </Button>
      </Stack>

      <Dialog
        open={saveDialogOpen}
        onClose={() => setSaveDialogOpen(false)}
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>Сохранить пресет</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            label="Название пресета"
            fullWidth
            value={newPresetName}
            onChange={(e) => setNewPresetName(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSavePreset()}
            sx={{mt: 1}}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSaveDialogOpen(false)}>Отмена</Button>
          <Button variant="contained" onClick={handleSavePreset} disabled={!newPresetName.trim()}>
            Сохранить
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

function fieldLabel(key: keyof PrintSettingsType): string {
  const labels: Record<keyof PrintSettingsType, string> = {
    labelWidthMm: "Ширина, мм",
    labelHeightMm: "Высота, мм",
    columns: "Колонки",
    gapMm: "Зазор, мм",
    pagePaddingMm: "Поля, мм",
    labelPaddingMm: "Отступ метки, мм",
    fontSizePx: "Размер текста, px",
  };
  return labels[key];
}

export default PrintSettings;
