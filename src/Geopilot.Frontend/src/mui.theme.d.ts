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
  }

  interface PaletteColor {
    contrast: string;
    states: PaletteColorStates;
    selected: string;
    hover: string;
  }

  interface SimplePaletteColorOptions {
    contrast?: string;
    states?: PaletteColorStates;
    selected?: string;
    hover?: string;
  }

  interface TypeBackground {
    base: string;
    content: string;
  }

  interface Palette {
    map: PaletteMap;
  }

  interface PaletteOptions {
    map?: PaletteMap;
  }
}
