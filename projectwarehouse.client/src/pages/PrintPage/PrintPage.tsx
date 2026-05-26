import {useState, useMemo} from "react";
import {useSearchParams} from "react-router";
import Box from "@mui/material/Box";
import GlobalStyles from "@mui/material/GlobalStyles";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import CloseIcon from "@mui/icons-material/Close";
import BarcodeLabel from "./BarcodeLabel.tsx";
import type {BarcodeType} from "./BarcodeLabel.tsx";
import PrintSettings from "./PrintSettings.tsx";
import {SYSTEM_PRESETS, loadCustomPresets, loadLastPresetId} from "./printPresets.ts";
import type {PrintPreset, PrintSettings as PrintSettingsType} from "./printPresets.ts";

const VALID_TYPES = new Set<string>(["DataMatrix", "EAN13", "Code128", "QR"]);

interface ParsedItem {
  type: BarcodeType;
  value: string;
  label?: string;
}

function parseItems(raw: string[]): ParsedItem[] {
  return raw.flatMap((s) => {
    const colonIdx = s.indexOf(":");
    if (colonIdx === -1) return [];
    const type = s.slice(0, colonIdx);
    const rest = s.slice(colonIdx + 1);
    const pipeIdx = rest.indexOf("|");
    const value = pipeIdx === -1 ? rest : rest.slice(0, pipeIdx);
    const label = pipeIdx === -1 ? undefined : rest.slice(pipeIdx + 1) || undefined;
    if (!VALID_TYPES.has(type) || !value) return [];
    return [{type: type as BarcodeType, value, label}];
  });
}

function resolveInitialPreset(customPresets: PrintPreset[]): {
  id: string;
  settings: PrintSettingsType;
} {
  const allPresets = [...SYSTEM_PRESETS, ...customPresets];
  const lastId = loadLastPresetId();
  const found = lastId ? allPresets.find((p) => p.id === lastId) : null;
  const preset = found ?? SYSTEM_PRESETS[0];
  return {id: preset.id, settings: {...preset.settings}};
}

function PrintPage() {
  const [searchParams] = useSearchParams();
  const [items, setItems] = useState(() => parseItems(searchParams.getAll("item")));

  const removeItem = (index: number) => setItems((prev) => prev.filter((_, i) => i !== index));

  const [customPresets, setCustomPresets] = useState<PrintPreset[]>(loadCustomPresets);
  const [{id: selectedId, settings}, setState] = useState(() =>
    resolveInitialPreset(customPresets),
  );

  const allPresets = useMemo(() => [...SYSTEM_PRESETS, ...customPresets], [customPresets]);

  const handlePresetSelect = (id: string) => {
    const preset = allPresets.find((p) => p.id === id);
    if (preset) setState({id, settings: {...preset.settings}});
  };

  const handleSettingsChange = (s: PrintSettingsType) => {
    setState({id: selectedId, settings: s});
  };

  const {labelWidthMm, labelHeightMm, columns, gapMm, pagePaddingMm, labelPaddingMm, fontSizePx} =
    settings;

  return (
    <Box sx={{minHeight: "100vh", bgcolor: "grey.100", "@media print": {bgcolor: "white"}}}>
      <GlobalStyles styles={{"@page": {margin: 0}}} />
      <PrintSettings
        presets={allPresets}
        selectedPresetId={selectedId}
        settings={settings}
        onPresetSelect={handlePresetSelect}
        onSettingsChange={handleSettingsChange}
        onCustomPresetsChange={setCustomPresets}
        customPresets={customPresets}
      />

      <Box
        sx={{
          p: `${pagePaddingMm}mm`,
          "@media print": {p: `${pagePaddingMm}mm`},
        }}
      >
        {items.length === 0 ? (
          <Box sx={{p: 4, textAlign: "center"}}>
            <Typography variant="h6" gutterBottom>
              Нет этикеток для печати
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Передайте параметры в URL:
            </Typography>
            <Typography
              variant="body2"
              component="code"
              sx={{
                display: "block",
                mt: 1,
                p: 1.5,
                bgcolor: "background.paper",
                borderRadius: 1,
                fontFamily: "monospace",
                fontSize: 12,
                wordBreak: "break-all",
              }}
            >
              {`/print?item=DataMatrix:ABC123|Товар А&item=EAN13:5901234123457&item=Code128:HELLO|Склад 3`}
            </Typography>
          </Box>
        ) : (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: `repeat(${columns}, ${labelWidthMm}mm)`,
              gap: `${gapMm}mm`,
            }}
          >
            {items.map((item, i) => (
              <Box
                key={`${item.type}:${item.value}:${i}`}
                sx={{position: "relative", "@media print": {"& .delete-btn": {display: "none"}}}}
              >
                <BarcodeLabel
                  type={item.type}
                  value={item.value}
                  label={item.label}
                  widthMm={labelWidthMm}
                  heightMm={labelHeightMm}
                  paddingMm={labelPaddingMm}
                  fontSizePx={fontSizePx}
                />
                <IconButton
                  className="delete-btn"
                  size="small"
                  onClick={() => removeItem(i)}
                  sx={{
                    position: "absolute",
                    top: -5,
                    right: -5,
                    bgcolor: "background.paper",
                    "&:hover": {bgcolor: "error.light", color: "error.contrastText"},
                  }}
                >
                  <CloseIcon fontSize="inherit" />
                </IconButton>
              </Box>
            ))}
          </Box>
        )}
      </Box>
    </Box>
  );
}

export default PrintPage;
