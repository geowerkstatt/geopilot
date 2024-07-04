import { useTranslation } from "react-i18next";
import { Box, Divider, Drawer, List, ListItem, ListItemButton, ListItemText, Toolbar } from "@mui/material";
import Header from "../../Header.jsx";
import { useState } from "react";
import { Outlet, useNavigate } from "react-router-dom";

export const Admin = props => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [adminMenuOpen, setAdminMenuOpen] = useState(false);
  const [isClosing, setIsClosing] = useState(false);
  const clientSettings = props.clientSettings;

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

  const navigateTo = path => {
    navigate(path);
    if (adminMenuOpen) {
      handleDrawerClose();
    }
  };

  const drawerWidth = "250px";
  const drawerContent = (
    <div>
      <Toolbar />
      <Box sx={{ overflow: "auto" }}>
        <List>
          <ListItem key={"deliveryOverview"} disablePadding>
            <ListItemButton
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
      <Header clientSettings={clientSettings} hasDrawerToggle={true} handleDrawerToggle={handleDrawerToggle} />
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", sm: "block" },
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { width: drawerWidth, boxSizing: "border-box", zIndex: 1100 },
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
      <Box sx={{ marginLeft: { xs: "0", sm: drawerWidth }, padding: "20px 35px", overflow: "auto" }}>
        <Outlet />
      </Box>
    </div>
  );
};

export default Admin;
