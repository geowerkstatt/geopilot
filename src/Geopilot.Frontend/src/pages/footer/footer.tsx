import { Box, Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

const Footer = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <Box
      sx={{
        width: `calc(100% - ${marginLeft})`,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        gap: "10px",
        marginLeft: { xs: "0", sm: marginLeft },
      }}
      className="footer">
      <Button
        onClick={() => {
          navigate("/privacyPolicy");
        }}>
        {t("privacyPolicy")}
      </Button>
      <Button
        onClick={() => {
          navigate("/impressum");
        }}>
        {t("impressum")}
      </Button>
      <Button
        onClick={() => {
          navigate("/about");
        }}>
        {t("about")}
      </Button>
    </Box>
  );
};

export default Footer;
