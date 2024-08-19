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
        width: { xs: "100%", md: `calc(100% - ${marginLeft})` },
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        flexWrap: "wrap",
        gap: "10px",
        marginLeft: { xs: "0", md: marginLeft },
        padding: "0 20px 10px 20px",
      }}
      className="footer">
      <Button
        data-cy="home-nav"
        onClick={() => {
          navigate("/");
        }}>
        geopilot
      </Button>
      <Button
        data-cy="privacy-policy-nav"
        onClick={() => {
          navigate("/privacy-policy");
        }}>
        {t("privacyPolicy")}
      </Button>
      <Button
        data-cy="imprint-nav"
        onClick={() => {
          navigate("/imprint");
        }}>
        {t("imprint")}
      </Button>
      <Button
        data-cy="about-nav"
        onClick={() => {
          navigate("/about");
        }}>
        {t("about")}
      </Button>
    </Box>
  );
};

export default Footer;
