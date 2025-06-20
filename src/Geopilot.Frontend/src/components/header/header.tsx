import { useTranslation } from "react-i18next";
import { FC, useState } from "react";
import { useLocation } from "react-router-dom";
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
import { FlexRowBox, FlexSpaceBetweenBox } from "../styledComponents.ts";
import { BaseButton } from "../buttons.tsx";
import { useControlledNavigate } from "../controlledNavigate";

interface HeaderProps {
  openSubMenu: () => void;
}

const Header: FC<HeaderProps> = ({ openSubMenu }) => {
  const { t, i18n } = useTranslation();
  const { navigateTo } = useControlledNavigate();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const { user, authEnabled, isAdmin, login, logout } = useGeopilotAuth();

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
      <AppBar sx={{ height: "60px", flex: "0" }}>
        <Toolbar
          sx={{
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <FlexRowBox
            data-cy="header"
            sx={{ padding: "5px 0", cursor: "pointer", flexWrap: "nowrap", overflow: "hidden" }}
            onClick={() => {
              navigateTo("/");
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
                display: {xs: "none", sm: "flex"},
                overflow: "hidden",
                textWrap: "nowrap",
                alignItems: { xs: "start", md: "center" },
              }}>
              <Typography sx={{ typography: { xs: "h4", md: "h1" }, margin: "0 !important" }}>
                geopilot&nbsp;
              </Typography>
              {clientSettings?.application?.name && (
                <Typography sx={{ typography: { xs: "h6", md: "h1" }, margin: "0 !important", textOverflow: "ellipsis", overflow: "hidden" }}>
                  {clientSettings?.application?.localName?.[i18n.language] || clientSettings?.application?.name}
                </Typography>
              )}
            </Box>
          </FlexRowBox>
          <FlexRowBox sx={{flexWrap: "nowrap"}}>
            <LanguagePopup />
            {authEnabled &&
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
        <FlexSpaceBetweenBox
          sx={{
            height: "100%",
            padding: "20px 0",
          }}>
          <Box
            sx={{ width: 300 }}
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
                    navigateTo("/");
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
                        navigateTo("/admin");
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
        </FlexSpaceBetweenBox>
      </Drawer>
    </>
  );
};

export default Header;
