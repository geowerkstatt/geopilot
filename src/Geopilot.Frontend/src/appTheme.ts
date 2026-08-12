import { createElement } from "react";
import CheckIcon from "@mui/icons-material/Check";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { alpha, createTheme, Shadows, ThemeOptions } from "@mui/material/styles";
import { Spacing } from "@mui/system";
import { themePalette } from "./appPalette";

const defaultTheme = createTheme();

const themeShadows: Shadows = [...defaultTheme.shadows];
const themeSpacing: Spacing = defaultTheme.spacing;

export const NEOGEO_LETTERSPACING = "0.05em";
export const px2rem = (pxValue: number) => `${pxValue / 16}rem`;

const DEFAULT_BORDER_RADIUS = 4;
const themeRadius: ThemeOptions["radius"] = {
  none: "0px",
  default: `${DEFAULT_BORDER_RADIUS}px`,
  full: "50%",
};

export const geopilotTheme = createTheme({
  palette: themePalette,
  shadows: themeShadows,
  spacing: themeSpacing,
  shape: { borderRadius: DEFAULT_BORDER_RADIUS },
  radius: themeRadius,
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
    allVariants: {
      letterSpacing: NEOGEO_LETTERSPACING,
    },
    body1: {
      fontSize: px2rem(16),
      lineHeight: 1.5,
    },
    body2: {
      fontSize: px2rem(14),
      lineHeight: 1.43,
    },
    caption: {
      fontSize: px2rem(12),
      lineHeight: 1.66,
      letterSpacing: "0.1em",
    },
    subtitle1: {
      fontSize: px2rem(16),
      lineHeight: 1.75,
    },
    subtitle2: {
      fontSize: px2rem(14),
      lineHeight: 1.57,
    },
    h1: {
      fontSize: px2rem(28),
      fontWeight: 600,
      lineHeight: 1.17,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
    },
    h2: {
      fontSize: px2rem(24),
      fontWeight: 600,
      lineHeight: 1.2,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
    },
    h3: {
      fontSize: px2rem(20),
      fontWeight: 600,
      lineHeight: 1.17,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
    },
    h4: {
      fontSize: px2rem(18),
      fontWeight: 600,
      lineHeight: 1.235,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
    },
    h5: {
      fontSize: px2rem(16),
      fontWeight: 600,
      lineHeight: 1.334,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
    },
    h6: {
      fontSize: px2rem(14),
      fontWeight: 600,
      lineHeight: 1.16,
      marginTop: px2rem(16),
      marginBottom: px2rem(8),
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
          fontSize: px2rem(20),
          lineHeight: px2rem(20),
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        size: "small",
      },
      styleOverrides: {
        root: {
          borderRadius: themeRadius.default,
          flex: "1",

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
      styleOverrides: {
        icon: {
          color: themePalette.primary.main,
        },
      },
    },
    MuiInputLabel: {
      defaultProps: {
        shrink: true,
      },
      styleOverrides: {
        root: {
          fontSize: px2rem(16),
          lineHeight: px2rem(16),
        },
      },
    },
    MuiOutlinedInput: {
      defaultProps: {
        notched: true,
      },
      styleOverrides: {
        notchedOutline: {
          borderColor: themePalette.primary.light,
        },
        input: {
          fontSize: px2rem(16),
          lineHeight: px2rem(24),
        },
        root: {
          "&:hover .MuiOutlinedInput-notchedOutline": {
            borderColor: themePalette.primary.main,
          },
          "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
            borderColor: themePalette.primary.main,
          },
          "&.Mui-error:hover .MuiOutlinedInput-notchedOutline": {
            borderColor: themePalette.error.dark,
          },
          "&.Mui-error.Mui-focused .MuiOutlinedInput-notchedOutline": {
            borderColor: themePalette.error.dark,
          },
        },
      },
    },
    MuiButton: {
      defaultProps: {
        color: "primary",
        variant: "outlined",
      },
      styleOverrides: {
        root: {
          textTransform: "none",
          fontWeight: "500",
          borderRadius: themeRadius.default,
          boxShadow: "none",
          "&:hover": {
            boxShadow: "none",
          },
          "&.MuiButton-outlined": {
            backgroundColor: themePalette.primary.contrast,
          },
          "&.MuiButton-outlined:hover": {
            backgroundColor: themePalette.primary.states.hover,
            borderColor: themePalette.primary.main,
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
          "&.MuiButton-sizeSmall .MuiButton-icon > :nth-of-type(1)": { fontSize: px2rem(18) },
          "&.MuiButton-sizeMedium .MuiButton-icon > :nth-of-type(1)": { fontSize: px2rem(20) },
          "&.MuiButton-sizeLarge .MuiButton-icon > :nth-of-type(1)": { fontSize: px2rem(22) },
        },
        sizeSmall: {
          fontSize: px2rem(14),
          lineHeight: px2rem(22),
        },
        sizeMedium: {
          fontSize: px2rem(16),
          lineHeight: px2rem(24),
        },
        sizeLarge: {
          fontSize: px2rem(16),
          lineHeight: px2rem(26),
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
        },
        colorPrimary: {
          color: themePalette.primary.main,
          "&:hover": {
            backgroundColor: "transparent",
            color: themePalette.primary.dark,
          },
          "&:disabled": {
            color: themePalette.primary.states.disabledBackground,
          },
        },
        colorPrimaryContained: {
          backgroundColor: themePalette.primary.main,
          color: themePalette.primary.contrast,
          borderRadius: themeRadius.default,
          "&:hover": {
            backgroundColor: themePalette.primary.dark,
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
          borderRadius: themeRadius.default,
          "&:hover": {
            border: `1px solid ${themePalette.primary.main}`,
            backgroundColor: themePalette.primary.states.hover,
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
    MuiButtonGroup: {
      styleOverrides: {
        root: {
          "&.MuiButtonGroup-vertical .MuiIconButton-root": {
            borderRadius: themeRadius.none,
            "&:not(:first-of-type)": {
              marginTop: "-1px",
            },
            "&:first-of-type": {
              borderTopLeftRadius: themeRadius.default,
              borderTopRightRadius: themeRadius.default,
            },
            "&:last-of-type": {
              borderBottomLeftRadius: themeRadius.default,
              borderBottomRightRadius: themeRadius.default,
            },
            "&:hover": {
              zIndex: 1,
            },
          },
        },
      },
    },
    MuiFormHelperText: {
      styleOverrides: {
        root: {
          fontStyle: "italic",
          fontSize: px2rem(12),
          lineHeight: 1.66,
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
          fontSize: px2rem(16),
          letterSpacing: NEOGEO_LETTERSPACING,
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
    MuiListItemButton: {
      styleOverrides: {
        root: {
          "&.Mui-selected": {
            backgroundColor: themePalette.primary.states.selected,
          },
          "&:hover": {
            backgroundColor: alpha(themePalette.primary.main, 0.04),
          },
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
          fontSize: px2rem(24),
          fontWeight: 600,
          letterSpacing: NEOGEO_LETTERSPACING,
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
          backgroundColor: defaultTheme.palette.grey[700],
          color: themePalette.primary.contrast,
          borderRadius: themeRadius.default,
          fontSize: px2rem(10),
          lineHeight: px2rem(14),
        },
        arrow: {
          color: defaultTheme.palette.grey[700],
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          backgroundColor: alpha(themePalette.primary.main, 0.2),

          "& .MuiChip-deleteIcon": {
            color: alpha(themePalette.primary.main, 0.4),

            "&:hover": {
              color: alpha(themePalette.primary.main, 0.6),
            },
          },
        },
        label: {
          fontSize: px2rem(13),
          lineHeight: px2rem(18),
        },
      },
    },
    MuiBadge: {
      styleOverrides: {
        badge: {
          fontSize: px2rem(12),
          lineHeight: px2rem(20),
        },
      },
    },
    MuiMenuItem: {
      styleOverrides: {
        root: {
          fontSize: px2rem(16),
          lineHeight: 1.5,
        },
      },
    },
    MuiToggleButton: {
      styleOverrides: {
        root: {
          color: themePalette.primary.main,
          padding: themeSpacing(1),
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
        sizeSmall: {
          fontSize: px2rem(14),
          lineHeight: px2rem(22),
        },
        sizeMedium: {
          fontSize: px2rem(16),
          lineHeight: px2rem(24),
        },
        sizeLarge: {
          fontSize: px2rem(16),
          lineHeight: px2rem(26),
        },
      },
    },
    MuiStack: { defaultProps: { gap: 2 } },
    MuiAlert: {
      defaultProps: {
        iconMapping: {
          success: createElement(CheckIcon, { fontSize: "inherit" }),
        },
      },
      styleOverrides: {
        outlined: ({ theme, ownerState }) => {
          const paletteColor = theme.palette[ownerState.severity ?? ownerState.color ?? "success"];
          return {
            color: paletteColor.contrastText,
            backgroundColor: paletteColor.background,
            borderColor: paletteColor.main,
          };
        },
        message: {
          fontSize: px2rem(14),
          lineHeight: 1.43,
        },
      },
    },
    MuiAccordion: {
      defaultProps: {
        disableGutters: true,
      },
      styleOverrides: {
        root: {
          boxShadow: "none",
          border: `1px solid ${themePalette.primary.light}`,
          borderRadius: themeRadius.none,
          borderTop: 0, // collapsed items stack: rely on the item above's bottom border
          "&:before": {
            display: "none",
          },
          // First of a collapsed run (list top, or right after an expanded item)
          "&:first-of-type, .Mui-expanded + &": {
            borderTop: `1px solid ${themePalette.primary.light}`,
            borderTopLeftRadius: themeRadius.default,
            borderTopRightRadius: themeRadius.default,
          },
          // Last of a collapsed run (list bottom, or right before an expanded item)
          "&:last-of-type, &:has(+ .Mui-expanded)": {
            borderBottomLeftRadius: themeRadius.default,
            borderBottomRightRadius: themeRadius.default,
          },
          // Expanded: detach into its own rounded card with vertical spacing
          "&.Mui-expanded": {
            borderTop: `1px solid ${themePalette.primary.light}`,
            borderRadius: themeRadius.default,
            marginTop: themeSpacing(2),
            marginBottom: themeSpacing(2),
            "&:first-of-type": { marginTop: 0 },
            "&:last-of-type": { marginBottom: 0 },
          },
        },
      },
    },
    MuiAccordionSummary: {
      styleOverrides: {
        expandIconWrapper: {
          color: themePalette.primary.main,
        },
      },
    },
  },
});
