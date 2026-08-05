import {createTheme} from "@mui/material";

const theme = createTheme({
  palette: {
    mode: "light",
    ozon: {
      main: '#005BFF',
      light: '#4D8CFF',
      dark: '#0041B8',
      contrastText: '#FFFFFF',
    },
    wb: {
      main: '#A70D73',
      light: '#C84BA0',
      dark: '#7A0954',
      contrastText: '#FFFFFF',
    },
  },
});

export default theme;
