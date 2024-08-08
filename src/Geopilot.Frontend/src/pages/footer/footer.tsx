import { Box, Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

const Footer = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();

  return (
    <Box sx={{ width: "100%", display: "flex", flexDirection: "row", justifyContent: "center", gap: "10px" }}>
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
