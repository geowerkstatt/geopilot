import { useTranslation } from "react-i18next";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText } from "@mui/material";
import { useState } from "react";
import { Outlet, useNavigate } from "react-router-dom";

const Admin = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [adminMenuOpen, setAdminMenuOpen] = useState(false);
  const [isClosing, setIsClosing] = useState(false);

  const handleDrawerClose = () => {
    setIsClosing(true);
    setAdminMenuOpen(false);
  };

  const handleDrawerTransitionEnd = () => {
    setIsClosing(false);
  };

  const handleDrawerToggle = () => {
    if (!isClosing) {
      setAdminMenuOpen(!adminMenuOpen);
    }
  };

  const navigateTo = (path: string) => {
    navigate(path);
    if (adminMenuOpen) {
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
      <Box sx={{ height: "60px" }} />
      <Divider />
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
      {/*<Header hasDrawerToggle={true} handleDrawerToggle={handleDrawerToggle} />*/}
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", sm: "block" },
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: "border-box", zIndex: 1000 },
        }}>
        {drawerContent}
      </Drawer>
      <Drawer
        variant="temporary"
        open={adminMenuOpen}
        onTransitionEnd={handleDrawerTransitionEnd}
        onClose={handleDrawerClose}
        ModalProps={{
          keepMounted: true,
        }}
        sx={{
          display: { xs: "block", sm: "none" },
          "& .MuiDrawer-paper": { boxSizing: "border-box" },
        }}>
        {drawerContent}
      </Drawer>
      <Box sx={{ height: "100%", marginLeft: { xs: "0", sm: drawerWidth }, padding: "20px 35px", overflow: "auto" }}>
        <Outlet />
      </Box>
    </div>
  );
};

export default Admin;
