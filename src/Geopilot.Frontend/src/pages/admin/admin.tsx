import { useTranslation } from "react-i18next";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText, Typography } from "@mui/material";
import { FC } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { FlexRowBox } from "../../components/styledComponents.ts";

interface AdminProps {
  isSubMenuOpen: boolean;
  setIsSubMenuOpen: (isOpen: boolean) => void;
}

const Admin: FC<AdminProps> = ({ isSubMenuOpen, setIsSubMenuOpen }) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { clientSettings } = useAppSettings();

  const handleDrawerClose = () => {
    setIsSubMenuOpen(false);
  };

  const navigateTo = (path: string) => {
    navigate(path);
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
  const drawerContent = (
    <div>
      <Box sx={{ overflow: "auto" }}>
        <List>
          <ListItem key={"deliveryOverview"} disablePadding>
            <ListItemButton
              selected={isActive("delivery-overview")}
              onClick={() => {
                navigateTo("delivery-overview");
              }}
              data-cy="admin-delivery-overview-nav">
              <ListItemText primary={t("deliveryOverview").toUpperCase()} />
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
                  navigateTo(link);
                }}
                data-cy={`admin-${link}-nav`}>
                <ListItemText primary={t(link).toUpperCase()} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      </Box>
    </div>
  );

  return (
    <Box sx={{ width: "100%" }}>
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", md: "block" },
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: "border-box", zIndex: 1000 },
        }}
        data-cy="admin-navigation">
        <>
          {" "}
          <Box sx={{ height: "60px" }} />
          <Divider />
          {drawerContent}
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
          "& .MuiDrawer-paper": { width: drawerWidth, boxSizing: "border-box" },
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
          {drawerContent}
        </>
      </Drawer>
      <Box
        sx={{
          height: "100%",
          marginLeft: { xs: "0", md: drawerWidth },
          overflow: "auto",
        }}>
        <Outlet />
      </Box>
    </Box>
  );
};

export default Admin;
