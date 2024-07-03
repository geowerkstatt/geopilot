import { useAuth } from "./auth";
import { useTranslation } from "react-i18next";
import * as React from "react";
import {
  AppBar,
  Box,
  Button,
  Divider,
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Toolbar,
  Typography,
} from "@mui/material";
import { LoggedInTemplate } from "./auth/LoggedInTemplate.jsx";
import { LoggedOutTemplate } from "./auth/LoggedOutTemplate.jsx";
import { AdminTemplate } from "./auth/AdminTemplate.jsx";
import { useLocation, useNavigate } from "react-router-dom";

export const Header = ({ clientSettings }) => {
  const { user, login, logout } = useAuth();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const [userMenuOpen, setUserMenuOpen] = React.useState(false);

  const toggleUserMenu = newOpen => () => {
    setUserMenuOpen(newOpen);
  };

  return (
    <header>
      <Box sx={{ flexGrow: 1 }}>
        <AppBar position="static">
          <Toolbar
            sx={{
              display: "flex",
              flexDirection: "row",
              justifyContent: "space-between",
            }}>
            <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center" }}>
              <img
                className="vendor-logo"
                src={clientSettings?.vendor?.logo}
                alt={`Logo of ${clientSettings?.vendor?.name}`}
                onError={e => {
                  e.target.style.display = "none";
                }}
              />
              <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
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
                <Button className="nav-button" sx={{ color: "white" }} onClick={toggleUserMenu(true)}>
                  {user?.name}
                </Button>
              </LoggedInTemplate>
            </Box>
          </Toolbar>
        </AppBar>
      </Box>
      <Drawer anchor={"right"} open={userMenuOpen} onClose={toggleUserMenu(false)}>
        <div className="user-menu">
          <Box
            sx={{ width: 250 }}
            role="presentation"
            onClick={toggleUserMenu(false)}
            onKeyDown={toggleUserMenu(false)}>
            <List>
              <ListItem key={user?.name}>
                <ListItemText primary={user?.name} />
              </ListItem>
            </List>
            <Divider />
            <List>
              <ListItem key={t("delivery").toUpperCase()} disablePadding>
                <ListItemButton
                  onClick={() => {
                    navigate("/");
                  }}>
                  <ListItemText primary={t("delivery").toUpperCase()} />
                </ListItemButton>
              </ListItem>
              <AdminTemplate>
                <ListItem key={t("administration").toUpperCase()} disablePadding>
                  <ListItemButton
                    onClick={() => {
                      navigate("/admin");
                    }}>
                    <ListItemText primary={t("administration").toUpperCase()} />
                  </ListItemButton>
                </ListItem>
                <ListItem key={t("stacBrowser").toUpperCase()} disablePadding>
                  <ListItemButton
                    onClick={() => {
                      window.location.href = "/browser";
                    }}>
                    <ListItemText primary={t("stacBrowser").toUpperCase()} />
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
    </header>
  );
};

export default Header;
