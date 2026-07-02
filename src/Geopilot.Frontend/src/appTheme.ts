import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { AppThemePalette, createTheme, Shadows } from "@mui/material/styles";
import { Spacing } from "@mui/system";

const defaultTheme = createTheme();

const themePalette: AppThemePalette = {
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
      focus: "#C6D4D5",
      focusVisible: "#B8C9CA",
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
};
const themeShadows: Shadows = [...defaultTheme.shadows];
const themeSpacing: Spacing = defaultTheme.spacing;

export const geopilotTheme = createTheme({
  palette: themePalette,
  shadows: themeShadows,
  spacing: themeSpacing,
  breakpoints: {
    values: {
      xs: 0,
      sm: 600,
      md: 1004,
      lg: 1200,
      xl: 1536,
    },
  },
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
          color: themePalette.text.primary,
          "&.Mui-disabled": {
            opacity: "60%",
            cursor: "default",
            color: themePalette.text.disabled,
          },
        },
      },
    },
    MuiAvatar: {
      styleOverrides: {
        root: {
          backgroundColor: themePalette.primary.main,
          color: themePalette.primary.contrast,
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        size: "small",
      },
      styleOverrides: {
        root: {
          borderRadius: themeSpacing(0.5),
          flex: "1",

          "& .MuiInputBase-input": {},

          "& .MuiSelect-select": {
            alignContent: "center",
          },

          "&.readonly": {
            pointerEvents: "none",
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
      defaultProps: {
        color: "primary",
      },
      styleOverrides: {
        root: {
          textTransform: "none",
          fontWeight: "500",
          borderRadius: themeSpacing(0.5),
          boxShadow: "none",
          "&:hover": {
            boxShadow: "none",
          },
          "&.MuiButton-outlined": {
            backgroundColor: themePalette.primary.contrast,
          },
          "&.Mui-disabled": {
            "&.MuiButton-text": {
              backgroundColor: "transparent",
              color: themePalette.primary.states.disabledBackground,
            },
            "&.MuiButton-contained": {
              backgroundColor: themePalette.primary.states.disabledBackground,
              color: themePalette.primary.contrast,
            },
            "&.MuiButton-outlined": {
              color: themePalette.primary.states.disabledBackground,
              borderColor: themePalette.primary.states.disabledBackground,
            },
          },
        },
      },
    },
    MuiIconButton: {
      defaultProps: {
        color: "primary",
      },
      styleOverrides: {
        root: {
          boxShadow: "none",
          "& .MuiTouchRipple-root": {
            display: "none",
          },
        },
        colorPrimary: {
          color: themePalette.primary.main,
          "&:hover": {
            backgroundColor: "transparent",
            color: themePalette.primary.dark,
          },
          "&:focus-visible": {
            backgroundColor: themePalette.primary.states.focusVisible,
          },
          "&:disabled": {
            color: themePalette.primary.states.disabledBackground,
          },
        },
        colorPrimaryContained: {
          backgroundColor: themePalette.primary.main,
          color: themePalette.primary.contrast,
          borderRadius: themeSpacing(0.5),
          "&:hover": {
            backgroundColor: themePalette.primary.dark,
          },
          "&:focus-visible": {
            backgroundColor: themePalette.primary.states.focusVisible,
          },
          "&:disabled": {
            backgroundColor: themePalette.primary.states.disabledBackground,
          },
        },
        colorPrimaryOutlined: {
          color: themePalette.primary.main,
          backgroundColor: themePalette.primary.contrast,
          padding: "7px",
          border: `1px solid ${themePalette.primary.light}`,
          borderRadius: themeSpacing(0.5),
          "&:hover": {
            border: `1px solid ${themePalette.primary.main}`,
            backgroundColor: themePalette.primary.contrast,
          },
          "&:focus-visible": {
            backgroundColor: themePalette.primary.states.focusVisible,
            border: `1px solid ${themePalette.primary.main}`,
          },
          "&:disabled": {
            color: themePalette.primary.states.disabledBackground,
            backgroundColor: themePalette.primary.contrast,
            border: `1px solid ${themePalette.primary.states.disabledBackground}`,
          },
          "&.active": {
            backgroundColor: themePalette.primary.states.selected,
          },
        },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: {
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
    MuiTooltip: {
      styleOverrides: {
        tooltip: {
          backgroundColor: "#616161",
          color: "#ffffff",
          borderRadius: themeSpacing(0.5),
        },
        arrow: {
          color: "#616161",
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          backgroundColor: "#124A4F33",

          "& .MuiChip-deleteIcon": {
            color: "#124A4F66",

            "&:hover": {
              color: "#124A4F99",
            },
          },
        },
      },
    },
    MuiToggleButton: {
      styleOverrides: {
        root: {
          color: themePalette.primary.main,
          borderColor: themePalette.primary.light,
          textTransform: "none",
          "&:hover": {
            backgroundColor: themePalette.primary.states.hover,
          },
          "&.Mui-selected, &.Mui-selected:hover": {
            color: themePalette.primary.main,
            backgroundColor: themePalette.primary.states.selected,
          },
        },
      },
    },
    MuiStack: { defaultProps: { gap: 2 } },
    MuiAccordion: {
      defaultProps: {
        disableGutters: true,
      },
      styleOverrides: {
        root: {
          boxShadow: "none",
          border: `1px solid ${themePalette.primary.light}`,
          borderRadius: themeSpacing(0.5),
          "&:before": {
            display: "none",
          },
        },
      },
    },
  },
});
