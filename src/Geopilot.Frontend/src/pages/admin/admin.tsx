import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Outlet } from "react-router-dom";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText, Stack, Typography } from "@mui/material";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { useControlledNavigate } from "../../components/controlledNavigate";
import { FlexRowBox } from "../../components/styledComponents.ts";

interface AdminProps {
  isSubMenuOpen: boolean;
  setIsSubMenuOpen: (isOpen: boolean) => void;
}

const Admin: FC<AdminProps> = ({ isSubMenuOpen, setIsSubMenuOpen }) => {
  const { t } = useTranslation();
  const { navigateTo } = useControlledNavigate();
  const { clientSettings } = useAppSettings();

  const handleDrawerClose = () => {
    setIsSubMenuOpen(false);
  };

  const navigate = (path: string) => {
    navigateTo(path);
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
              selected={isActive("delivery-overview")}
              onClick={() => {
                navigate("/admin/delivery-overview");
              }}
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
                selected={isActive(link)}
                onClick={() => {
                  navigate("/admin/" + link);
                }}
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
          <FlexRowBox
            sx={{ padding: "8px 16px", cursor: "pointer" }}
            onClick={() => {
              navigate("/");
            }}>
            {clientSettings?.application?.logo && (
              <Box>
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
                flexDirection: "column",
                alignItems: "start",
              }}>
              <Typography variant="h4" sx={{ margin: "0 !important" }}>
                geopilot&nbsp;
              </Typography>
              {clientSettings?.application?.name && (
                <Typography variant="h6" sx={{ margin: "0 !important" }}>
                  {clientSettings?.application?.name}
                </Typography>
              )}
            </Box>
          </FlexRowBox>
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
  );
};

export default Admin;
