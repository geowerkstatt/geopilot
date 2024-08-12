import { useTranslation } from "react-i18next";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText, Typography } from "@mui/material";
import { FC } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";

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
              }}>
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
                }}>
                <ListItemText primary={t(link).toUpperCase()} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      </Box>
    </div>
  );

  return (
    <div className="admin">
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", md: "block" },
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: "border-box", zIndex: 1000 },
        }}>
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
          <Box sx={{ display: "flex", flexDirection: "row", alignItems: "center", padding: "8px 16px" }}>
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
                marginLeft: "20px",
                display: "flex",
                flexDirection: { xs: "column", md: "row" },
                alignItems: { xs: "start", md: "center" },
              }}>
              <Typography sx={{ typography: { xs: "h4", md: "h1" } }}>geopilot</Typography>
              {clientSettings?.application?.name && (
                <Typography sx={{ typography: { xs: "h6", md: "h1" } }}>{clientSettings?.application?.name}</Typography>
              )}
            </Box>
          </Box>
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
    </div>
  );
};

export default Admin;
