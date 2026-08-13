// This import makes the file a module so the `declare module` blocks below augment
// MUI's types instead of replacing them. It also pulls in the x-data-grid theme
// augmentation required to type the MuiDataGrid entry in appTheme.ts.
import type {} from "@mui/x-data-grid/themeAugmentation";

declare module "@mui/material/IconButton" {
  interface IconButtonPropsColorOverrides {
    primaryContained: true;
    primaryOutlined: true;
  }

  interface IconButtonClasses {
    colorPrimaryContained: string;
    colorPrimaryOutlined: string;
  }
}

declare module "@mui/material/styles" {
  interface PaletteColorStates {
    hover: string;
    selected: string;
    disabledBackground: string;
  }

  interface PaletteMap {
    fill: string;
    stroke: string;
    highlight: string;
    hintBackground: string;
    hintText: string;
  }

  interface PaletteColor {
    contrast: string;
    states: PaletteColorStates;
    selected: string;
    hover: string;
    background: string;
  }

  interface SimplePaletteColorOptions {
    contrast?: string;
    states?: PaletteColorStates;
    selected?: string;
    hover?: string;
    background?: string;
  }

  interface TypeBackground {
    base: string;
    content: string;
  }

  interface Palette {
    map: PaletteMap;
    tooltip: { background: string };
  }

  interface PaletteOptions {
    map?: PaletteMap;
    tooltip?: { background: string };
  }

  interface Theme {
    radius: { none: string; default: string; full: string };
  }

  interface ThemeOptions {
    radius?: { none: string; default: string; full: string };
  }
}
