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
    stroke: string;
    hint: { background: string; text: string };
    error: { fill: string; highlight: string };
    diff: { added: string; changed: string; deleted: string };
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

  interface PaletteDiff {
    added: string;
    changed: string;
    deleted: string;
    insertBackground: string;
    deleteBackground: string;
  }

  interface Palette {
    map: PaletteMap;
    diff: PaletteDiff;
    tooltip: { background: string };
  }

  interface PaletteOptions {
    map?: PaletteMap;
    diff?: PaletteDiff;
    tooltip?: { background: string };
  }

  interface Theme {
    radius: { none: string; default: string; full: string };
  }

  interface ThemeOptions {
    radius?: { none: string; default: string; full: string };
  }
}
