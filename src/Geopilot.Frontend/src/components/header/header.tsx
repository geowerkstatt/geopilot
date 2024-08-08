import { useTranslation } from "react-i18next";
import { useState } from "react";
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

const Header = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const { user, isAdmin, isLoggedIn, login, logout } = useGeopilotAuth();

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
      <AppBar sx={{ height: "60px" }}>
        <Toolbar
          sx={{
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center", padding: "5px 0" }}>
            {clientSettings?.application?.logo && (
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
            <Typography variant="h1" sx={{ marginLeft: "20px" }}>
              geopilot {clientSettings?.application?.name}
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 0, gap: "20px" }}>
            <LanguagePopup />
            {isLoggedIn ? (
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
                onClick={toggleUserMenu(true)}>
                <Avatar
                  sx={{
                    backgroundColor: "primary.main",
                    color: "primary.contrastText",
                  }}>
                  {user?.fullName[0].toUpperCase()}
                </Avatar>
              </IconButton>
            ) : (
              <Button onClick={login} startIcon={<LoginIcon />}>
                {t("logIn")}
              </Button>
            )}
          </Box>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)}>
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
                  }}>
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
                </>
              )}
            </List>
          </Box>
          <Button className="nav-button" sx={{ color: "black" }} onClick={logout} startIcon={<LogoutIcon />}>
            {t("logOut")}
          </Button>
        </Box>
      </Drawer>
    </>
  );
};

export default Header;
