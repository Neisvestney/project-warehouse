export interface PrintSettings {
  labelWidthMm: number;
  labelHeightMm: number;
  columns: number;
  gapMm: number;
  pagePaddingMm: number;
  labelPaddingMm: number;
}

export interface PrintPreset {
  id: string;
  name: string;
  settings: PrintSettings;
  isCustom?: boolean;
}

export const SYSTEM_PRESETS: PrintPreset[] = [
  {
    id: "thermal-58x40mm",
    name: "Термо 58x40мм",
    settings: {
      labelWidthMm: 58,
      labelHeightMm: 40,
      columns: 1,
      gapMm: 0,
      pagePaddingMm: 0,
      labelPaddingMm: 5,
    },
  },
  {
    id: "a4-4x7",
    name: "A4, 4×7",
    settings: {
      labelWidthMm: 48,
      labelHeightMm: 38,
      columns: 4,
      gapMm: 3,
      pagePaddingMm: 10,
      labelPaddingMm: 1,
    },
  },
  {
    id: "a4-2x5",
    name: "A4, 2×5",
    settings: {
      labelWidthMm: 90,
      labelHeightMm: 58,
      columns: 2,
      gapMm: 4,
      pagePaddingMm: 10,
      labelPaddingMm: 1,
    },
  },
  {
    id: "a5-2x4",
    name: "A5, 2×4",
    settings: {
      labelWidthMm: 60,
      labelHeightMm: 37,
      columns: 2,
      gapMm: 3,
      pagePaddingMm: 8,
      labelPaddingMm: 1,
    },
  },
];

const CUSTOM_PRESETS_KEY = "print-page-presets";
const LAST_PRESET_KEY = "print-page-last-preset";

export function loadCustomPresets(): PrintPreset[] {
  try {
    const raw = localStorage.getItem(CUSTOM_PRESETS_KEY);
    return raw ? (JSON.parse(raw) as PrintPreset[]) : [];
  } catch {
    return [];
  }
}

export function saveCustomPresets(presets: PrintPreset[]): void {
  localStorage.setItem(CUSTOM_PRESETS_KEY, JSON.stringify(presets));
}

export function loadLastPresetId(): string | null {
  return localStorage.getItem(LAST_PRESET_KEY);
}

export function saveLastPresetId(id: string): void {
  localStorage.setItem(LAST_PRESET_KEY, id);
}
