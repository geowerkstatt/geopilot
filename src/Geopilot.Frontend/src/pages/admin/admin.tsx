import { FC, MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import { Outlet, Link as RouterLink, useLocation } from "react-router-dom";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText, Stack, Typography } from "@mui/material";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { useControlledLinkClick } from "../../components/controlledNavigate";
import { PageContent } from "../../components/styledComponents.ts";
import { useApplicationName } from "../../hooks/useApplicationName.ts";

interface AdminProps {
  isSubMenuOpen: boolean;
  setIsSubMenuOpen: (isOpen: boolean) => void;
}

const Admin: FC<AdminProps> = ({ isSubMenuOpen, setIsSubMenuOpen }) => {
  const { t } = useTranslation();
  const linkClick = useControlledLinkClick();
  const location = useLocation();
  const { clientSettings } = useAppSettings();
  const applicationName = useApplicationName();

  const handleDrawerClose = () => {
    setIsSubMenuOpen(false);
  };

  const handleNavClick = (path: string) => (event: MouseEvent<HTMLElement>) => {
    linkClick(path)(event);
    if (isSubMenuOpen) {
      handleDrawerClose();
    }
  };

  const isActive = (path: string) => {
    if (path === "") {
      return location.pathname === "/";
    }
    return location.pathname.split("/").includes(path);
  };

  const drawerWidth = "250px";
  const drawerContent = (isPermanent: boolean) => (
    <div>
      <Box sx={{ overflow: "auto" }}>
        <List>
          <ListItem key={"deliveryOverview"} disablePadding>
            <ListItemButton
              component={RouterLink}
              to="/admin/delivery-overview"
              selected={isActive("delivery-overview")}
              onClick={handleNavClick("/admin/delivery-overview")}
              data-cy={isPermanent ? "admin-delivery-overview-nav" : undefined}>
              <ListItemText primary={t("deliveryOverview")} />
            </ListItemButton>
          </ListItem>
        </List>
        <Divider />
        <List>
          {["users", "mandates", "organisations"].map(link => (
            <ListItem key={link} disablePadding>
              <ListItemButton
                component={RouterLink}
                to={"/admin/" + link}
                selected={isActive(link)}
                onClick={handleNavClick("/admin/" + link)}
                data-cy={isPermanent ? `admin-${link}-nav` : undefined}>
                <ListItemText primary={t(link)} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      </Box>
    </div>
  );

  return (
    <PageContent>
      <Box sx={{ width: "100%", height: "100%" }}>
        <Drawer
          variant="permanent"
          sx={{
            display: { xs: "none", md: "block" },
            width: drawerWidth,
            flexShrink: 0,
            [`& .MuiDrawer-paper`]: {
              width: drawerWidth,
              zIndex: 1000,
              borderColor: theme => theme.palette.primary.light,
            },
          }}
          data-cy="admin-navigation">
          <>
            <Box sx={{ height: "60px" }} />
            {drawerContent(true)}
          </>
        </Drawer>
        <Drawer
          variant="temporary"
          open={isSubMenuOpen}
          onClose={handleDrawerClose}
          ModalProps={{
            keepMounted: true,
          }}
          sx={{
            display: { xs: "block", md: "none" },
            width: drawerWidth,
            "& .MuiDrawer-paper": { width: drawerWidth },
          }}>
          <>
            <Stack
              component={RouterLink}
              to="/"
              direction="row"
              onClick={handleNavClick("/")}
              sx={{
                py: 1,
                px: 2,
                alignItems: "center",
                flexWrap: "wrap",
                textDecoration: "none",
                color: "inherit",
              }}>
              {clientSettings?.application?.logo && (
                <Box>
                  <img
                    src={clientSettings?.application?.logo}
                    alt={`Logo of ${applicationName}`}
                    style={{ maxHeight: "40px", cursor: "pointer" }}
                  />
                </Box>
              )}
              <Stack sx={{ alignItems: "start", gap: 0 }}>
                <Typography variant="h4" sx={{ margin: "0 !important" }}>
                  geopilot&nbsp;
                </Typography>
                {applicationName && (
                  <Typography variant="h6" sx={{ margin: "0 !important" }}>
                    {applicationName}
                  </Typography>
                )}
              </Stack>
            </Stack>
            {drawerContent(false)}
          </>
        </Drawer>
        <Stack
          sx={{
            height: "100%",
            marginLeft: { xs: "0", md: drawerWidth },
          }}>
          <Outlet />
        </Stack>
      </Box>
    </PageContent>
  );
};

export default Admin;
