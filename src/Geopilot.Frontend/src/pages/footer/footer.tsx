import { useTranslation } from "react-i18next";
import { Button, useMediaQuery, useTheme } from "@mui/material";
import { useControlledNavigate } from "../../components/controlledNavigate";
import { FlexRowBox } from "../../components/styledComponents.ts";

const Footer = () => {
  const { t } = useTranslation();
  const { navigateTo } = useControlledNavigate();
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <FlexRowBox
      sx={{
        justifyContent: "center",
        flexWrap: "wrap",
        marginLeft: { xs: "0", md: marginLeft },
        padding: "0 20px 10px 20px",
      }}
      className="footer">
      <Button
        size={isXs ? "small" : "medium"}
        data-cy="home-nav"
        onClick={() => {
          navigateTo("/");
        }}>
        geopilot
      </Button>
      <Button
        size={isXs ? "small" : "medium"}
        data-cy="privacy-policy-nav"
        onClick={() => {
          navigateTo("/privacy-policy");
        }}>
        {t("privacyPolicy")}
      </Button>
      <Button
        size={isXs ? "small" : "medium"}
        data-cy="imprint-nav"
        onClick={() => {
          navigateTo("/imprint");
        }}>
        {t("imprint")}
      </Button>
      <Button
        size={isXs ? "small" : "medium"}
        data-cy="about-nav"
        onClick={() => {
          navigateTo("/about");
        }}>
        {t("about")}
      </Button>
    </FlexRowBox>
  );
};

export default Footer;
