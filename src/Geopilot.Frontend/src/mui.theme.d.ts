import { Theme, ThemeOptions } from "@mui/material/styles";

declare module "@mui/material/styles" {
  interface CustomTheme extends Theme {
    palette: {
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
      };
      warning: {
        main: string;
      };
      error: {
        main: string;
      };
    };
    components: {
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
    };
  }
  // allow configuration using `createTheme`
  interface CustomThemeOptions extends ThemeOptions {
    palette?: {
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
      };
      warning: {
        main: string;
      };
      error: {
        main: string;
      };
    };
    components?: {
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
    };
  }
  export function createTheme(options?: CustomThemeOptions): CustomTheme;
}
