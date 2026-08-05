import '@mui/material/styles';
import '@mui/material/Chip';

declare module '@mui/material/styles' {
  interface Palette {
    ozon: Palette['primary'];
    wb: Palette['primary'];
  }
  interface PaletteOptions {
    ozon?: PaletteOptions['primary'];
    wb?: PaletteOptions['primary'];
  }
}

declare module '@mui/material/Chip' {
  interface ChipPropsColorOverrides {
    ozon: true;
    wb: true;
  }
}