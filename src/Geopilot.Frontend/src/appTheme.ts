import { createTheme } from "@mui/material/styles";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

export const geopilotTheme = createTheme({
  palette: {
    primary: {
      main: "#3A6060",
      hover: "#3A60600A",
      contrastText: "#ffffff",
    },
    secondary: {
      main: "#00ff97",
      hover: "#00ff970A",
      contrastText: "#000",
    },
    warning: {
      main: "#fd9903",
    },
    error: {
      main: "#e53835",
    },
    success: {
      main: "#4caf51",
    },
  },
  typography: {
    fontFamily: "NeoGeo, sans-serif",
    h1: {
      fontSize: "28px",
      fontWeight: 600,
    },
    h2: {
      fontSize: "26px",
      fontWeight: 600,
    },
    h3: {
      fontSize: "24px",
      fontWeight: 600,
    },
    h4: {
      fontSize: "22px",
      fontWeight: 600,
    },
    h5: {
      fontSize: "20px",
      fontWeight: 600,
    },
    h6: {
      fontSize: "14px",
      fontWeight: 600,
    },
  },
  components: {
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
  },
});
