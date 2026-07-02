import { Typography } from "@mui/material";
import { Shadows, ThemeOptions } from "@mui/material/styles";
import { TypographyOptions } from "@mui/material/styles/createTypography";
import { Spacing } from "@mui/system";

declare module "@mui/material/IconButton" {
  interface IconButtonPropsColorOverrides {
    primaryInverse: true;
  }
}

declare module "@mui/material/styles" {
  export interface AppThemePalette {
    text: {
      primary: string;
      secondary: string;
      disabled: string;
    };
    primary: {
      main: string;
      dark: string;
      light: string;
      contrast: string;
      states: {
        hover: string;
        selected: string;
        focus: string;
        focusVisible: string;
        disabledBackground: string;
      };
    };
    secondary: {
      main: string;
    };
    background: {
      base: string;
      content: string;
    };
    success: {
      main: string;
      hover: string;
    };
    warning: {
      main: string;
      hover: string;
    };
    error: {
      main: string;
      selected: string;
      hover: string;
    };
  }

  interface AppThemeComponents extends Components {
    MuiTypography: object;
    MuiAvatar: object;
    MuiTextField: object;
    MuiSelect: object;
    MuiButtonBase: object;
    MuiButton: object;
    MuiIconButton: object;
    MuiAppBar: object;
    MuiDataGrid: object;
    MuiStepLabel: object;
    MuiStepContent: object;
    MuiListItemText: object;
    MuiDialog: object;
    MuiDialogTitle: object;
    MuiDialogContent: object;
    MuiDialogActions: object;
    MuiTooltip: object;
    MuiChip: object;
    MuiToggleButton: object;
    MuiStack: object;
    MuiAccordion: object;
  }

  interface AppThemeComponentsOptions extends ComponentsOptions {
    MuiTypography: object;
    MuiAvatar: object;
    MuiTextField: object;
    MuiSelect: object;
    MuiButtonBase: object;
    MuiButton: object;
    MuiIconButton: object;
    MuiAppBar: object;
    MuiDataGrid: object;
    MuiStepLabel: object;
    MuiStepContent: object;
    MuiListItemText: object;
    MuiDialog: object;
    MuiDialogTitle: object;
    MuiDialogContent: object;
    MuiDialogActions: object;
    MuiTooltip: object;
    MuiChip: object;
    MuiToggleButton: object;
    MuiStack: object;
    MuiAccordion: object;
  }

  interface AppTheme extends Theme {
    spacing: Spacing;
    shadows: Shadows;
    palette: AppThemePalette;
    typography: Typography;
    components: AppThemeComponents;
  }

  interface AppThemeOptions extends ThemeOptions {
    spacing: Spacing;
    shadows: Shadows;
    palette: AppThemePalette;
    typography: TypographyOptions;
    components: AppThemeComponentsOptions;
  }

  export function createTheme(options?: AppThemeOptions): AppTheme;
  export function createTheme(theme: AppTheme, ...args: object[]): AppTheme;
}
