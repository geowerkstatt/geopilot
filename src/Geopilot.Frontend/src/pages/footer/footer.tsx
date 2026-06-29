import { useTranslation } from "react-i18next";
import { Button, Stack, useMediaQuery, useTheme } from "@mui/material";
import { useControlledNavigate } from "../../components/controlledNavigate";

const Footer = () => {
  const { t } = useTranslation();
  const { navigateTo } = useControlledNavigate();
  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <Stack
      direction="row"
      pb={1}
      px={3}
      sx={{
        alignItems: "center",
        justifyContent: "center",
        flexWrap: "wrap",
        marginLeft: { xs: "0", md: marginLeft },
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
    </Stack>
  );
};

export default Footer;
