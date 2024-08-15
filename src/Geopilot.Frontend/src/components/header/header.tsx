import { useTranslation } from "react-i18next";
import { FC, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  AppBar,
  Avatar,
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
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import LoginIcon from "@mui/icons-material/Login";
import LogoutIcon from "@mui/icons-material/Logout";
import { useAppSettings } from "../appSettings/appSettingsInterface";
import { useGeopilotAuth } from "../../auth";
import { LanguagePopup } from "./languagePopup";
import MenuIcon from "@mui/icons-material/Menu";

interface HeaderProps {
  openSubMenu: () => void;
}

const Header: FC<HeaderProps> = ({ openSubMenu }) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const { user, enabled, isAdmin, login, logout } = useGeopilotAuth();

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
  const hasSubMenu = location.pathname.startsWith("/admin");

  return (
    <>
      <AppBar sx={{ height: "60px" }}>
        <Toolbar
          sx={{
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center", padding: "5px 0" }}>
            <Box sx={{ display: { xs: "block", md: "none" }, flex: "0", marginRight: "10px" }}>
              {hasSubMenu ? (
                <IconButton
                  sx={{ paddingLeft: "0" }}
                  color="inherit"
                  onClick={() => {
                    openSubMenu();
                  }}>
                  <MenuIcon fontSize="large" />
                </IconButton>
              ) : (
                <Box>
                  <img
                    src={clientSettings?.application?.logo}
                    alt={`Logo of ${clientSettings?.application?.name}`}
                    style={{ maxHeight: "40px", cursor: "pointer" }}
                    onClick={() => {
                      window.open(clientSettings?.application?.url, "_blank");
                    }}
                  />
                </Box>
              )}
            </Box>
            {clientSettings?.application?.logo && (
              <Box sx={{ display: { xs: "none", md: "block" }, marginRight: "20px" }}>
                <img
                  src={clientSettings?.application?.logo}
                  alt={`Logo of ${clientSettings?.application?.name}`}
                  style={{ maxHeight: "40px", cursor: "pointer" }}
                  onClick={() => {
                    window.open(clientSettings?.application?.url, "_blank");
                  }}
                />
              </Box>
            )}
            <Box
              sx={{
                display: "flex",
                flexDirection: { xs: "column", md: "row" },
                alignItems: { xs: "start", md: "center" },
              }}>
              <Typography sx={{ typography: { xs: "h4", md: "h1" } }}>geopilot&nbsp;</Typography>
              {clientSettings?.application?.name && (
                <Typography sx={{ typography: { xs: "h6", md: "h1" } }}>{clientSettings?.application?.name}</Typography>
              )}
            </Box>
          </Box>
          <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center" }}>
            <LanguagePopup />
            {enabled &&
              (user ? (
                <IconButton
                  sx={{
                    padding: "0",
                    "&:hover, &.Mui-focusVisible, &:active, &:focus, &:focus-visible": {
                      backgroundColor: "rgba(0, 0, 0, 0.0)",
                    },
                    "& .MuiTouchRipple-root": {
                      display: "none",
                    },
                  }}
                  onClick={toggleUserMenu(true)}
                  data-cy="loggedInUser-button">
                  <Avatar
                    sx={{
                      backgroundColor: "primary.main",
                      color: "primary.contrastText",
                    }}>
                    {user?.fullName[0].toUpperCase()}
                  </Avatar>
                </IconButton>
              ) : (
                <>
                  <Button
                    onClick={login}
                    startIcon={<LoginIcon />}
                    sx={{ display: { xs: "none", md: "flex" } }}
                    data-cy="login-button">
                    {t("logIn")}
                  </Button>
                  <IconButton onClick={login} sx={{ display: { xs: "flex", md: "none" } }} color="primary">
                    <LoginIcon />
                  </IconButton>
                </>
              ))}
          </Box>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)} data-cy="tool-navigation">
        <Box
          sx={{
            display: "flex;",
            flexDirection: "column",
            justifyContent: "space-between",
            height: "100%",
            padding: "20px 0",
          }}>
          <Box
            sx={{ width: 250 }}
            role="presentation"
            onClick={toggleUserMenu(false)}
            onKeyDown={toggleUserMenu(false)}>
            <List>
              <ListItem key={user?.fullName}>
                <ListItemText primary={user?.fullName} secondary={user?.email} />
              </ListItem>
            </List>
            <Divider />
            <List>
              <ListItem key={t("delivery").toUpperCase()} disablePadding>
                <ListItemButton
                  selected={isActive("")}
                  onClick={() => {
                    navigate("/");
                  }}
                  data-cy="delivery-nav">
                  <ListItemText primary={t("delivery").toUpperCase()} />
                </ListItemButton>
              </ListItem>
              {isAdmin && (
                <>
                  <ListItem key={t("administration").toUpperCase()} disablePadding>
                    <ListItemButton
                      selected={isActive("admin")}
                      onClick={() => {
                        navigate("/admin");
                      }}
                      data-cy="admin-nav">
                      <ListItemText primary={t("administration").toUpperCase()} />
                    </ListItemButton>
                  </ListItem>
                  <ListItem key={t("stacBrowser").toUpperCase()} disablePadding>
                    <ListItemButton
                      selected={isActive("browser")}
                      onClick={() => {
                        window.open("/browser", "_blank");
                      }}
                      data-cy="stacBrowser-nav">
                      <ListItemText primary={t("stacBrowser").toUpperCase()} />
                      <ListItemIcon>
                        <OpenInNewIcon fontSize="small" />
                      </ListItemIcon>
                    </ListItemButton>
                  </ListItem>
                </>
              )}
            </List>
          </Box>
          <Button
            variant="contained"
            sx={{ margin: "0 20px" }}
            onClick={logout}
            startIcon={<LogoutIcon />}
            data-cy="logout-button">
            {t("logOut")}
          </Button>
        </Box>
      </Drawer>
    </>
  );
};

export default Header;
