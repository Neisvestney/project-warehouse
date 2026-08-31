import {createTheme} from "@mui/material";
import type {CSSObject} from "@mui/material";

export const APP_BAR_DARK_BG = "#1c1c22";
// MUI's default primary.main, which the light AppBar inherits as its ground.
export const APP_BAR_LIGHT_BG = "#1976d2";

type ChipTint = {bg: string; fg: string; hover: string};

// Muted grounds for dark-scheme chips, so a chip reads as a label rather than a button.
const DARK_CHIP_TINTS = {
  neutral: {bg: "#2A2A33", fg: "rgba(255, 255, 255, 0.75)", hover: "#33333D"},
  blue: {bg: "#12303D", fg: "#4FC3F7", hover: "#173D4E"},
  indigo: {bg: "#12283D", fg: "#90CAF9", hover: "#17344E"},
  purple: {bg: "#331B3A", fg: "#CE93D8", hover: "#402247"},
  orange: {bg: "#402A12", fg: "#FFB74D", hover: "#4F3517"},
  green: {bg: "#1E3A24", fg: "#81C784", hover: "#26492E"},
  red: {bg: "#3B1F1F", fg: "#EF9A9A", hover: "#4A2727"},
} as const satisfies Record<string, ChipTint>;

// Item types own their hues instead of borrowing the built-ins: statuses and order kinds already claim
// all six, and an order row shows a kind, a status and an item type side by side.
const ITEM_CHIP_TINTS = {
  standard: {bg: "#1E2833", fg: "#A8C4DC", hover: "#27333F"},
  unit: {bg: "#17222F", fg: "#7BA7CE", hover: "#1F2E3E"},
  productGroup: {bg: "#241C3D", fg: "#B39DDB", hover: "#2E2450"},
  variation: {bg: "#331B12", fg: "#E8A182", hover: "#40241A"},
  bundle: {bg: "#10331F", fg: "#4ADE80", hover: "#164229"},
} as const satisfies Record<string, ChipTint>;

// `dark` is what Chip uses on hover, so here it is lighter than `main`, not darker.
const tintPalette = (tint: ChipTint, light: string) => ({
  main: tint.bg,
  light,
  dark: tint.hover,
  contrastText: tint.fg,
});

const BUILT_IN_CHIP_TINTS: Record<string, ChipTint> = {
  Default: DARK_CHIP_TINTS.neutral,
  Primary: DARK_CHIP_TINTS.indigo,
  Secondary: DARK_CHIP_TINTS.purple,
  Error: DARK_CHIP_TINTS.red,
  Warning: DARK_CHIP_TINTS.orange,
  Info: DARK_CHIP_TINTS.blue,
  Success: DARK_CHIP_TINTS.green,
};

const darkChipOverrides = (): CSSObject =>
  Object.entries(BUILT_IN_CHIP_TINTS).reduce<CSSObject>((acc, [name, tint]) => {
    acc[`&.MuiChip-filled.MuiChip-color${name}`] = {
      backgroundColor: tint.bg,
      color: tint.fg,
      "&:hover": {backgroundColor: tint.hover},
      "& .MuiChip-icon": {color: tint.fg},
      "& .MuiChip-deleteIcon": {color: tint.fg, "&:hover": {color: tint.fg}},
    };
    return acc;
  }, {});

const theme = createTheme({
  colorSchemes: {
    light: {
      palette: {
        ozon: {
          main: "#005BFF",
          light: "#4D8CFF",
          dark: "#0041B8",
          contrastText: "#FFFFFF",
        },
        wb: {
          main: "#A70D73",
          light: "#C84BA0",
          dark: "#7A0954",
          contrastText: "#FFFFFF",
        },
        itemStandard: {
          main: "#4A6D8C",
          light: "#6B8BA8",
          dark: "#35526B",
          contrastText: "#FFFFFF",
        },
        // standard and unit share a hue and differ only in tone — together they are PHYSICAL_CATALOG_ITEMS.
        itemUnit: {
          main: "#144786",
          light: "#3A6091",
          dark: "#153157",
          contrastText: "#FFFFFF",
        },
        itemProductGroup: {
          main: "#5E35B1",
          light: "#7E57C2",
          dark: "#4527A0",
          contrastText: "#FFFFFF",
        },
        itemVariation: {
          main: "#d5603a",
          light: "#B96C51",
          dark: "#913c20",
          contrastText: "#FFFFFF",
        },
        itemBundle: {
          main: "#00875A",
          light: "#26A96C",
          dark: "#00653F",
          contrastText: "#FFFFFF",
        },
      },
    },
    dark: {
      palette: {
        // primary: {
        //   main: "#9BBA2C",
        // },
        background: {
          default: "#16161A",
          paper: "#19191e",
        },
        text: {
          primary: "rgba(255, 255, 255, 0.92)",
          secondary: "rgba(255, 255, 255, 0.60)",
          disabled: "rgba(255, 255, 255, 0.38)",
        },
        error: {
          main: "#E57373",
          light: "#EF9A9A",
          dark: "#D32F2F",
          contrastText: "rgba(0, 0, 0, 0.87)",
        },
        ozon: {
          main: "#4D8CFF",
          light: "#84AFFF",
          dark: "#005BFF",
          contrastText: "#0A1020",
        },
        wb: {
          main: "#D45BAC",
          light: "#E68AC7",
          dark: "#A70D73",
          contrastText: "#1A0A14",
        },
        itemStandard: tintPalette(ITEM_CHIP_TINTS.standard, "#33455A"),
        itemUnit: tintPalette(ITEM_CHIP_TINTS.unit, "#2A3D52"),
        itemProductGroup: tintPalette(ITEM_CHIP_TINTS.productGroup, "#3A2E62"),
        itemVariation: tintPalette(ITEM_CHIP_TINTS.variation, "#4E2C1E"),
        itemBundle: tintPalette(ITEM_CHIP_TINTS.bundle, "#1D5232"),
      },
    },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        html: {
          overflowY: "scroll",
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        // MUI's elevation overlay compounds on nested Paper and lightens it cascade-style.
        // Depth reads from the shadow instead, and `paper` stays exactly its palette color.
        root: ({theme}) => theme.applyStyles("dark", {backgroundImage: "none"}),
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: ({theme}) =>
          theme.applyStyles("dark", {
            backgroundColor: APP_BAR_DARK_BG,
            backgroundImage: "none",
            boxShadow: "none",
            borderBottom: `1px solid ${theme.palette.divider}`,
          }),
      },
    },
    MuiChip: {
      styleOverrides: {
        root: ({theme}) => theme.applyStyles("dark", darkChipOverrides()),
      },
    },
    MuiTableCell: {
      styleOverrides: {
        // MUI derives this border as darken(opaque divider, 0.68) = #515151, heavier than every other
        // line in the app — those all sit on `divider` itself.
        root: ({theme}) => theme.applyStyles("dark", {borderBottomColor: theme.palette.divider}),
      },
    },
    MuiListItemIcon: {
      styleOverrides: {
        // `action.active` is pure white in the dark scheme, making list icons brighter than the labels
        // beside them. It is 54% black in light, so align on `text.secondary`.
        root: ({theme}) => theme.applyStyles("dark", {color: theme.palette.text.secondary}),
      },
    },
  },
});

export default theme;
