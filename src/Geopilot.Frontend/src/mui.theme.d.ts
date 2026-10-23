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

  interface PaletteMapFeature {
    fill: string;
    stroke: string;
    highlight: string;
  }

  interface PaletteMap extends PaletteMapFeature {
    hintBackground: string;
    hintText: string;
    /** The features of the old state of a diff. */
    old: PaletteMapFeature;
    /** The features of the new state of a diff. */
    new: PaletteMapFeature;
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
    /** Text and icons of what a diff added. */
    added: string;
    /** Text and icons of what a diff removed. */
    deleted: string;
    /** Behind the added part of a value. */
    insertBackground: string;
    /** Behind the removed part of a value. */
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
