import { FC, useState } from "react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router-dom";
import MenuIcon from "@mui/icons-material/Menu";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
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
  Stack,
  Toolbar,
  Typography,
} from "@mui/material";
import { useGeopilotAuth } from "../../auth";
import { useAppSettings } from "../appSettings/appSettingsInterface";
import { BaseButton } from "../buttons.tsx";
import { useControlledNavigate } from "../controlledNavigate";
import { LanguagePopup } from "./languagePopup";

interface HeaderProps {
  openSubMenu: () => void;
}

const Header: FC<HeaderProps> = ({ openSubMenu }) => {
  const { t, i18n } = useTranslation();
  const { navigateTo } = useControlledNavigate();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const { user, authLoaded, isAdmin, login, logout } = useGeopilotAuth();

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
      <AppBar
        sx={{
          height: "60px",
          flex: "0",
          borderBottom: theme => `1px solid ${theme.palette.primary.light}`,
          backgroundColor: "white",
        }}>
        <Toolbar
          sx={{
            height: "60px",
            minHeight: "auto !important",
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <Stack
            data-cy="header"
            direction="row"
            sx={{
              alignItems: "center",
              cursor: "pointer",
              overflow: "hidden",
            }}
            onClick={() => {
              navigateTo("/");
            }}>
            <Box sx={{ display: { xs: "block", md: "none", maxHeight: "40px" } }}>
              {hasSubMenu ? (
                <IconButton
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
              <Box sx={{ display: { xs: "none", md: "block" }, maxHeight: "40px" }}>
                <img
                  src={clientSettings?.application?.logo}
                  alt={`Logo of ${clientSettings?.application?.name}`}
                  style={{ maxHeight: "40px", cursor: "pointer" }}
                />
              </Box>
            )}
            <Box
              sx={{
                display: { xs: "none", sm: "flex" },
                overflow: "hidden",
                textWrap: "nowrap",
                alignItems: { xs: "start", md: "center" },
              }}>
              <Typography sx={{ typography: { xs: "h4", md: "h1" }, margin: "0 !important" }}>
                geopilot&nbsp;
              </Typography>
              {clientSettings?.application?.name && (
                <Typography
                  sx={{
                    typography: { xs: "h6", md: "h1" },
                    margin: "0 !important",
                    textOverflow: "ellipsis",
                    overflow: "hidden",
                  }}>
                  {clientSettings?.application?.localName?.[i18n.language] || clientSettings?.application?.name}
                </Typography>
              )}
            </Box>
          </Stack>
          <Stack direction="row" alignItems="center">
            <LanguagePopup />
            {authLoaded &&
              (user ? (
                <IconButton
                  sx={{
                    p: 0,
                  }}
                  onClick={toggleUserMenu(true)}
                  data-cy="loggedInUser-button">
                  <Avatar>{user?.fullName[0].toUpperCase()}</Avatar>
                </IconButton>
              ) : (
                <>
                  <BaseButton variant="text" onClick={login} label="logIn" />
                </>
              ))}
          </Stack>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)} data-cy="tool-navigation">
        <Stack
          py={2}
          sx={{
            justifyContent: "space-between",
            height: "100%",
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
              <ListItem key="delivery" disablePadding>
                <ListItemButton
                  selected={isActive("")}
                  onClick={() => {
                    navigateTo("/");
                  }}
                  data-cy="delivery-nav">
                  <ListItemText primary={t("delivery")} />
                </ListItemButton>
              </ListItem>
              <ListItem key="myDeliveries" disablePadding>
                <ListItemButton
                  selected={isActive("user")}
                  onClick={() => {
                    navigateTo("/user/deliveries");
                  }}
                  data-cy="my-deliveries-nav">
                  <ListItemText primary={t("myDeliveries")} />
                </ListItemButton>
              </ListItem>
              {isAdmin && (
                <>
                  <ListItem key="administration" disablePadding>
                    <ListItemButton
                      selected={isActive("admin")}
                      onClick={() => {
                        navigateTo("/admin");
                      }}
                      data-cy="admin-nav">
                      <ListItemText primary={t("administration")} />
                    </ListItemButton>
                  </ListItem>
                  <ListItem key="stacBrowser" disablePadding>
                    <ListItemButton
                      selected={isActive("browser")}
                      onClick={() => {
                        window.open("/browser", "_blank");
                      }}
                      data-cy="stacBrowser-nav">
                      <ListItemText primary={t("stacBrowser")} />
                      <ListItemIcon>
                        <OpenInNewIcon fontSize="small" />
                      </ListItemIcon>
                    </ListItemButton>
                  </ListItem>
                </>
              )}
            </List>
          </Box>
          <BaseButton sx={{ mx: 2 }} onClick={logout} label="logOut" />
        </Stack>
      </Drawer>
    </>
  );
};

export default Header;
