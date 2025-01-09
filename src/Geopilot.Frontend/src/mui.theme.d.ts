import { Typography } from "@mui/material";
import { Theme, ThemeOptions } from "@mui/material/styles";
import { TypographyOptions } from "@mui/material/styles/createTypography";

declare module "@mui/material/styles" {
  export interface AppThemePalette {
    primary: {
      main: string;
      inactive: string;
      hover: string;
      contrastText: string;
    };
    secondary: {
      main: string;
      inactive: string;
      hover: string;
      contrastText: string;
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
      hover: string;
    };
  }

  interface AppThemeComponents extends Components {
    MuiTypography: object;
    MuiAvatar: object;
    MuiFormControl: object;
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
  }

  interface AppThemeComponentsOptions extends ComponentsOptions {
    MuiTypography: object;
    MuiAvatar: object;
    MuiFormControl: object;
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
  }

  interface AppTheme extends Theme {
    palette: AppThemePalette;
    typography: Typography;
    components: AppThemeComponents;
  }

  interface AppThemeOptions extends ThemeOptions {
    palette: AppThemePalette;
    typography: TypographyOptions;
    components: AppThemeComponentsOptions;
  }

  export function createTheme(options?: AppThemeOptions): AppTheme;
}
