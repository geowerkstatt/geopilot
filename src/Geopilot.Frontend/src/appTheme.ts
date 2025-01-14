import { AppThemePalette, createTheme, Shadows } from "@mui/material/styles";
import { Spacing } from "@mui/system";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

const defaultTheme = createTheme();

const themePalette: AppThemePalette = {
  primary: {
    main: "#124A4F",
    inactive: "#124A4F99",
    hover: "#124A4F0D",
    contrastText: "#ffffff",
  },
  secondary: {
    main: "#00ff97",
    inactive: "#00ff9799",
    hover: "#00ff970D",
    contrastText: "#000",
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
    hover: "#e538350D",
  },
};
const themeShadows: Shadows = [...defaultTheme.shadows];
const themeSpacing: Spacing = defaultTheme.spacing;

export const geopilotTheme = createTheme({
  palette: themePalette,
  shadows: themeShadows,
  spacing: themeSpacing,
  typography: {
    fontFamily: "NeoGeo, sans-serif",
    body1: {
      fontSize: "16px",
      letterSpacing: "0.05em",
    },
    body2: {
      fontSize: "14px",
      letterSpacing: "0.05em",
    },
    caption: {
      letterSpacing: "0.1em",
    },
    button: {
      fontSize: "16px",
      letterSpacing: "0.05em",
    },
    h1: {
      fontSize: "28px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
    h2: {
      fontSize: "24px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
    h3: {
      fontSize: "20px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
    h4: {
      fontSize: "18px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
    h5: {
      fontSize: "16px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
    h6: {
      fontSize: "14px",
      fontWeight: 600,
      letterSpacing: "0.05em",
      marginTop: "1rem",
      marginBottom: "0.5rem",
    },
  },
  components: {
    MuiTypography: {
      styleOverrides: {
        root: {
          "&.Mui-disabled": {
            opacity: "60%",
            cursor: "default",
          },
        },
      },
    },
    MuiAvatar: {
      styleOverrides: {
        root: {
          backgroundColor: themePalette.primary.main,
          color: themePalette.primary.contrastText,
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        size: "small",
        variant: "filled",
      },
      styleOverrides: {
        root: {
          borderRadius: themeSpacing(0.5),
          flex: "1",

          "&.readonly": {
            pointerEvents: "none",
          },
        },
      },
    },
    MuiFormControl: {
      styleOverrides: {
        root: {
          "& .MuiFilledInput-root": {
            backgroundColor: "rgba(0,0,0,0.04)",
          },
          "& .MuiFilledInput-root:hover:not(.Mui-disabled, .Mui-error):before": {
            borderColor: themePalette.primary.main,
          },
          "& .MuiFilledInput-root:not(.Mui-error):before": {
            borderColor: themePalette.primary.main,
          },
          "& .MuiFilledInput-root:not(.Mui-error):after": {
            borderColor: themePalette.primary.main,
          },
        },
      },
    },
    MuiSelect: {
      defaultProps: {
        IconComponent: ExpandMoreIcon,
      },
    },
    MuiButtonBase: {
      defaultProps: {
        disableRipple: true,
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          fontWeight: "500",
          borderRadius: "4px",
          boxShadow: "none",
          "&:hover": {
            boxShadow: "none",
          },
          "&.Mui-disabled": {
            "&.MuiButton-text": {
              backgroundColor: "transparent",
              color: themePalette.primary.inactive,
            },
            "&.MuiButton-contained": {
              backgroundColor: themePalette.primary.inactive,
              color: themePalette.primary.contrastText,
            },
            "&.MuiButton-outlined": {
              backgroundColor: "transparent",
              color: themePalette.primary.inactive,
              borderColor: themePalette.primary.inactive,
            },
          },
        },
      },
    },
    MuiIconButton: {
      styleOverrides: {
        root: {
          "&:hover, &.Mui-focusVisible, &:active, &:focus, &:focus-visible": {
            backgroundColor: "rgba(0, 0, 0, 0.0)",
          },
          "& .MuiTouchRipple-root": {
            display: "none",
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
          backgroundColor: "#ffffffd5",
          color: "#000",
          boxShadow: "none",
        },
      },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: {
          height: "auto",
          "& .MuiTablePagination-toolbar p": {
            margin: "auto",
          },
        },
      },
    },
    MuiStepLabel: {
      styleOverrides: {
        label: {
          fontSize: "16px",
          letterSpacing: "0.05em",
          "&.Mui-active": {
            fontWeight: 600,
          },
          "&.Mui-completed": {
            fontWeight: 600,
          },
        },
      },
    },
    MuiStepContent: {
      styleOverrides: {
        root: {
          padding: "24px 0 0 40px",
        },
      },
    },
    MuiListItemText: {
      styleOverrides: {
        root: {
          overflow: "hidden",
        },
        primary: {
          overflowWrap: "break-word",
        },
        secondary: {
          overflowWrap: "break-word",
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          padding: themeSpacing(2),
        },
      },
    },
    MuiDialogTitle: {
      styleOverrides: {
        root: {
          fontSize: "24px",
          fontWeight: 600,
          letterSpacing: "0.05em",
          padding: "0",
          paddingBottom: themeSpacing(1),
          margin: "0",
        },
      },
    },
    MuiDialogContent: {
      styleOverrides: {
        root: {
          padding: "0",
          paddingBottom: themeSpacing(1),
        },
      },
    },
    MuiDialogActions: {
      styleOverrides: {
        root: {
          padding: "0",
          paddingTop: themeSpacing(1),
          "& > :not(:first-of-type)": {
            marginLeft: themeSpacing(2),
          },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          backgroundColor: "#124A4F33",

          "& .MuiChip-deleteIcon": {
            color: "#124A4F80",
          },
        },
      },
    },
  },
});
