import { FC, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation } from "react-router-dom";
import MenuIcon from "@mui/icons-material/Menu";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import {
  AppBar,
  Avatar,
  Box,
  Divider,
  Drawer,
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
import { useApplicationName } from "../../hooks/useApplicationName";
import { useAppSettings } from "../appSettings/appSettingsInterface";
import { Button, IconButton } from "../buttons.tsx";
import { useControlledLinkClick } from "../controlledNavigate";
import { LanguagePopup } from "./languagePopup";

interface HeaderProps {
  openSubMenu: () => void;
}

const Header: FC<HeaderProps> = ({ openSubMenu }) => {
  const { t } = useTranslation();
  const linkClick = useControlledLinkClick();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const applicationName = useApplicationName();
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
          backgroundColor: theme => theme.palette.background.content,
        }}>
        <Toolbar
          sx={{
            height: "60px",
            minHeight: "auto !important",
            display: "flex",
            flexDirection: "row",
            justifyContent: "space-between",
          }}>
          <Stack direction="row" sx={{ alignItems: "center", overflow: "hidden" }}>
            {hasSubMenu && (
              <Box
                sx={{
                  display: { xs: "flex", md: "none" },
                  maxHeight: "40px",
                  justifyContent: "center",
                  alignItems: "center",
                }}>
                <IconButton icon={<MenuIcon fontSize="large" />} label="menu" onClick={openSubMenu} />
              </Box>
            )}
            <Stack
              component={RouterLink}
              to="/"
              onClick={linkClick("/")}
              data-cy="header"
              direction="row"
              sx={{
                alignItems: "center",
                overflow: "hidden",
                textDecoration: "none",
                color: "inherit",
              }}>
              {!hasSubMenu && clientSettings?.application?.logo && (
                <Box
                  sx={{
                    display: { xs: "flex", md: "none" },
                    maxHeight: "40px",
                    justifyContent: "center",
                    alignItems: "center",
                  }}>
                  <img
                    src={clientSettings?.application?.logo}
                    alt={`Logo of ${applicationName}`}
                    style={{ maxHeight: "40px", cursor: "pointer" }}
                  />
                </Box>
              )}
              {clientSettings?.application?.logo && (
                <Box sx={{ display: { xs: "none", md: "block" }, maxHeight: "40px" }}>
                  <img
                    src={clientSettings?.application?.logo}
                    alt={`Logo of ${applicationName}`}
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
                {applicationName && (
                  <Typography
                    sx={{
                      pt: { xs: 0.25, md: 0 },
                      typography: { xs: "h6", md: "h1" },
                      m: "0 !important",
                      textOverflow: "ellipsis",
                      overflow: "hidden",
                    }}>
                    {applicationName}
                  </Typography>
                )}
              </Box>
            </Stack>
          </Stack>
          <Stack direction="row" sx={{ alignItems: "center" }}>
            <LanguagePopup />
            {authLoaded &&
              (user ? (
                <Avatar
                  onClick={toggleUserMenu(true)}
                  data-cy="loggedInUser-button"
                  sx={{ cursor: "pointer", "&:hover": { backgroundColor: "primary.dark" } }}>
                  {user?.fullName[0].toUpperCase()}
                </Avatar>
              ) : (
                <>
                  <Button variant="text" onClick={login} label="logIn" />
                </>
              ))}
          </Stack>
        </Toolbar>
      </AppBar>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)} data-cy="tool-navigation">
        <Stack
          sx={{
            py: 2,
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
                  component={RouterLink}
                  to="/"
                  selected={isActive("")}
                  onClick={linkClick("/")}
                  data-cy="delivery-nav">
                  <ListItemText primary={t("delivery")} />
                </ListItemButton>
              </ListItem>
              <ListItem key="myDeliveries" disablePadding>
                <ListItemButton
                  component={RouterLink}
                  to="/user/deliveries"
                  selected={isActive("user")}
                  onClick={linkClick("/user/deliveries")}
                  data-cy="my-deliveries-nav">
                  <ListItemText primary={t("myDeliveries")} />
                </ListItemButton>
              </ListItem>
              {isAdmin && (
                <>
                  <ListItem key="administration" disablePadding>
                    <ListItemButton
                      component={RouterLink}
                      to="/admin"
                      selected={isActive("admin")}
                      onClick={linkClick("/admin")}
                      data-cy="admin-nav">
                      <ListItemText primary={t("administration")} />
                    </ListItemButton>
                  </ListItem>
                  <ListItem key="stacBrowser" disablePadding>
                    <ListItemButton
                      component="a"
                      href="/browser"
                      target="_blank"
                      rel="noopener"
                      selected={isActive("browser")}
                      data-cy="stacBrowser-nav">
                      <ListItemText primary={t("stacBrowser")} />
                      <ListItemIcon sx={{ justifyContent: "flex-end" }}>
                        <OpenInNewIcon fontSize="small" sx={{ color: "primary.main" }} />
                      </ListItemIcon>
                    </ListItemButton>
                  </ListItem>
                </>
              )}
            </List>
          </Box>
          <Button variant="contained" sx={{ mx: 2 }} onClick={logout} label="logOut" />
        </Stack>
      </Drawer>
    </>
  );
};

export default Header;
