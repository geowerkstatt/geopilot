import { alpha, PaletteOptions } from "@mui/material/styles";

export const themePalette = {
  text: {
    primary: "#212121",
    secondary: "#666666",
    disabled: "#9E9E9E",
  },
  primary: {
    main: "#124A4F",
    dark: "#0C3337",
    light: "#89A4A7",
    contrast: "#ffffff",
    states: {
      hover: "#EDF1F1",
      selected: "#D4DEDF",
      disabledBackground: "#719295",
    },
  },
  secondary: {
    main: "#00FF97",
  },
  background: {
    base: "#F6F8F8",
    content: "#ffffff",
  },
  success: {
    main: "#4caf51",
    dark: "#1B5E20",
    light: "#4CAF50",
    contrastText: "#1E4620",
    background: "#EDF7ED",
    hover: alpha("#4caf51", 0.05),
  },
  info: {
    main: "#0288D1",
    dark: "#01579B",
    light: "#03A9F4",
    contrastText: "#014361",
    background: "#E5F6FD",
  },
  warning: {
    main: "#fd9903",
    dark: "#F57C00",
    light: "#FFB74D",
    contrastText: "#663C00",
    background: "#FFF4E5",
    selected: alpha("#fd9903", 0.18),
    hover: alpha("#fd9903", 0.05),
  },
  error: {
    main: "#e53835",
    dark: "#C62828",
    light: "#EF5350",
    contrastText: "#5F2120",
    background: "#FDEDED",
    selected: alpha("#e53835", 0.18),
    hover: alpha("#e53835", 0.05),
  },
  map: {
    fill: "#e53835",
    stroke: "#ffffff",
    highlight: "#980303",
    hintBackground: alpha("#000000", 0.6),
    hintText: "#ffffff",
  },
  divider: alpha("#124A4F", 0.2),
  tooltip: {
    background: "#424242",
  },
} satisfies PaletteOptions;
