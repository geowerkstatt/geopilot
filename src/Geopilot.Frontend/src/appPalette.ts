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
    hover: "#4caf510D",
  },
  warning: {
    main: "#fd9903",
    hover: "#fd99030D",
  },
  error: {
    main: "#e53835",
    selected: "#e538352E",
    hover: "#e538350D",
  },
  map: {
    fill: "#e53835",
    stroke: "#ffffff",
  },
  divider: alpha("#124A4F", 0.2),
} satisfies PaletteOptions;
