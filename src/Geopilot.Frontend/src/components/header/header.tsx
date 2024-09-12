import { useTranslation } from "react-i18next";
import { FC, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  AppBar,
  Avatar,
  Box,
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
import { FlexColumnSpaceBetweenBox, FlexRowBox } from "../styledComponents.ts";
import { BaseButton } from "../buttons.tsx";

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
          <FlexRowBox
            sx={{ padding: "5px 0", cursor: "pointer" }}
            onClick={() => {
              navigate("/");
            }}>
            <Box sx={{ display: { xs: "block", md: "none" }, flex: "0", marginRight: "10px" }}>
              {hasSubMenu ? (
                <IconButton
                  sx={{ paddingLeft: "0" }}
                  color="inherit"
                  onClick={e => {
                    e.stopPropagation();
                    openSubMenu();
                  }}>
                  <MenuIcon fontSize="large" />
                </IconButton>
              ) : (
                clientSettings?.application?.logo && (
                  <Box>
                    <img
                      src={clientSettings?.application?.logo}
                      alt={`Logo of ${clientSettings?.application?.name}`}
                      style={{ maxHeight: "40px", cursor: "pointer" }}
                    />
                  </Box>
                )
              )}
            </Box>
            {clientSettings?.application?.logo && (
              <Box sx={{ display: { xs: "none", md: "block" }, marginRight: "20px" }}>
                <img
                  src={clientSettings?.application?.logo}
                  alt={`Logo of ${clientSettings?.application?.name}`}
                  style={{ maxHeight: "40px", cursor: "pointer" }}
                />
              </Box>
            )}
            <Box
              sx={{
                display: "flex",
                flexDirection: { xs: "column", md: "row" },
                alignItems: { xs: "start", md: "center" },
              }}>
              <Typography sx={{ typography: { xs: "h4", md: "h1" }, margin: "0 !important" }}>
                geopilot&nbsp;
              </Typography>
              {clientSettings?.application?.name && (
                <Typography sx={{ typography: { xs: "h6", md: "h1" }, margin: "0 !important" }}>
                  {clientSettings?.application?.name}
                </Typography>
              )}
            </Box>
          </FlexRowBox>
          <FlexRowBox>
            <LanguagePopup />
            {enabled &&
              (user ? (
                <IconButton
                  sx={{
                    padding: "0",
                  }}
                  onClick={toggleUserMenu(true)}
                  data-cy="loggedInUser-button">
                  <Avatar>{user?.fullName[0].toUpperCase()}</Avatar>
                </IconButton>
              ) : (
                <>
                  <BaseButton
                    variant="text"
                    onClick={login}
                    icon={<LoginIcon />}
                    sx={{ display: { xs: "none", md: "flex" } }}
                    label="logIn"
                  />
                  <IconButton onClick={login} sx={{ display: { xs: "flex", md: "none" } }} color="primary">
                    <LoginIcon />
                  </IconButton>
                </>
              ))}
          </FlexRowBox>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)} data-cy="tool-navigation">
        <FlexColumnSpaceBetweenBox
          sx={{
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
          <BaseButton sx={{ margin: "0 20px" }} onClick={logout} icon={<LogoutIcon />} label="logOut" />
        </FlexColumnSpaceBetweenBox>
      </Drawer>
    </>
  );
};

export default Header;
