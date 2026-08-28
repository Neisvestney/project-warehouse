import "@mui/material/styles";
import "@mui/material/Chip";

declare module "@mui/material/styles" {
  interface Palette {
    ozon: Palette["primary"];
    wb: Palette["primary"];
    itemStandard: Palette["primary"];
    itemUnit: Palette["primary"];
    itemProductGroup: Palette["primary"];
    itemVariation: Palette["primary"];
    itemBundle: Palette["primary"];
  }
  interface PaletteOptions {
    ozon?: PaletteOptions["primary"];
    wb?: PaletteOptions["primary"];
    itemStandard?: PaletteOptions["primary"];
    itemUnit?: PaletteOptions["primary"];
    itemProductGroup?: PaletteOptions["primary"];
    itemVariation?: PaletteOptions["primary"];
    itemBundle?: PaletteOptions["primary"];
  }
}

declare module "@mui/material/Chip" {
  interface ChipPropsColorOverrides {
    ozon: true;
    wb: true;
    itemStandard: true;
    itemUnit: true;
    itemProductGroup: true;
    itemVariation: true;
    itemBundle: true;
  }
}
