import { useGeopilotAuth } from "./auth";
import { useTranslation } from "react-i18next";
import {
  AppBar,
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import AccountCircleOutlinedIcon from "@mui/icons-material/AccountCircleOutlined";
import { LoggedInTemplate } from "./auth/LoggedInTemplate.js";
import { LoggedOutTemplate } from "./auth/LoggedOutTemplate.js";
import { AdminTemplate } from "./auth/AdminTemplate.js";
import { useLocation, useNavigate } from "react-router-dom";
import { ClientSettings } from "./AppInterfaces";
import { FC, useState } from "react";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

interface HeaderProps {
  clientSettings: ClientSettings;
  hasDrawerToggle?: boolean;
  handleDrawerToggle?: () => void;
}

export const Header: FC<HeaderProps> = ({ clientSettings, hasDrawerToggle, handleDrawerToggle }) => {
  const { user, login, logout } = useGeopilotAuth();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [userMenuOpen, setUserMenuOpen] = useState<boolean>(false);

  const toggleUserMenu = (newOpen: boolean) => () => {
    setUserMenuOpen(newOpen);
  };

  const isActive = (path: string) => {
    if (path === "") {
      return location.pathname === "/";
    }
    return location.pathname.split("/").includes(path);
  };

  return (
    <>
      <AppBar>
        <Toolbar
          sx={{
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center" }}>
            {hasDrawerToggle ? (
              <>
                <IconButton
                  color="inherit"
                  aria-label="open drawer"
                  edge="start"
                  onClick={handleDrawerToggle}
                  sx={{ mr: 2, display: { sm: "none" } }}>
                  <MenuIcon fontSize="large" />
                </IconButton>
                <Box sx={{ display: { xs: "none", sm: "block" } }}>
                  <img
                    className="vendor-logo"
                    src={clientSettings?.vendor?.logo}
                    alt={`Logo of ${clientSettings?.vendor?.name}`}
                    onError={e => {
                      const img = e.target as HTMLImageElement;
                      img.style.display = "none";
                    }}
                  />
                </Box>
              </>
            ) : (
              <img
                className="vendor-logo"
                src={clientSettings?.vendor?.logo}
                alt={`Logo of ${clientSettings?.vendor?.name}`}
                onError={e => {
                  const img = e.target as HTMLImageElement;
                  img.style.display = "none";
                }}
              />
            )}
            <Typography variant="h6" component="div" sx={{ display: { xs: "none", sm: "block" }, flexGrow: 1 }}>
              {location.pathname.includes("admin") ? t("administration").toUpperCase() : t("delivery").toUpperCase()}
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 0 }}>
            <LoggedOutTemplate>
              <Button className="nav-button" sx={{ color: "white" }} onClick={login}>
                {t("logIn")}
              </Button>
            </LoggedOutTemplate>
            <LoggedInTemplate>
              <IconButton className="nav-button" sx={{ color: "white" }} onClick={toggleUserMenu(true)}>
                <AccountCircleOutlinedIcon fontSize="large" />
              </IconButton>
            </LoggedInTemplate>
          </Box>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)}>
        <div className="user-menu">
          <Box
            sx={{ width: 250 }}
            role="presentation"
            onClick={toggleUserMenu(false)}
            onKeyDown={toggleUserMenu(false)}>
            <List>
              <ListItem key={user?.fullName}>
                <ListItemText primary={user?.fullName} />
              </ListItem>
            </List>
            <Divider />
            <List>
              <ListItem key={t("delivery").toUpperCase()} disablePadding>
                <ListItemButton
                  selected={isActive("")}
                  onClick={() => {
                    navigate("/");
                  }}>
                  <ListItemText primary={t("delivery").toUpperCase()} />
                </ListItemButton>
              </ListItem>
              <AdminTemplate>
                <ListItem key={t("administration").toUpperCase()} disablePadding>
                  <ListItemButton
                    selected={isActive("admin")}
                    onClick={() => {
                      navigate("/admin");
                    }}>
                    <ListItemText primary={t("administration").toUpperCase()} />
                  </ListItemButton>
                </ListItem>
                <ListItem key={t("stacBrowser").toUpperCase()} disablePadding>
                  <ListItemButton
                    selected={isActive("browser")}
                    onClick={() => {
                      window.open("/browser", "_blank");
                    }}>
                    <ListItemText primary={t("stacBrowser").toUpperCase()} />
                    <ListItemIcon>
                      <OpenInNewIcon fontSize="small" />
                    </ListItemIcon>
                  </ListItemButton>
                </ListItem>
              </AdminTemplate>
            </List>
          </Box>
          <Button className="nav-button" sx={{ color: "black" }} onClick={logout}>
            {t("logOut")}
          </Button>
        </div>
      </Drawer>
    </>
  );
};

export default Header;
