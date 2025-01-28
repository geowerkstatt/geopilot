import { Button } from "@mui/material";
import { useTranslation } from "react-i18next";
import { FlexRowCenterBox } from "../../components/styledComponents.ts";
import { useControlledNavigate } from "../../components/controlledNavigate";

const Footer = () => {
  const { t } = useTranslation();
  const { navigateTo } = useControlledNavigate();

  const isAdminRoute = location.pathname.startsWith("/admin");
  const marginLeft = isAdminRoute ? "250px" : "0";

  return (
    <FlexRowCenterBox
      sx={{
        flexWrap: "wrap",
        marginLeft: { xs: "0", md: marginLeft },
        padding: "0 20px 10px 20px",
      }}
      className="footer">
      <Button
        data-cy="home-nav"
        onClick={() => {
          navigateTo("/");
        }}>
        geopilot
      </Button>
      <Button
        data-cy="privacy-policy-nav"
        onClick={() => {
          navigateTo("/privacy-policy");
        }}>
        {t("privacyPolicy")}
      </Button>
      <Button
        data-cy="imprint-nav"
        onClick={() => {
          navigateTo("/imprint");
        }}>
        {t("imprint")}
      </Button>
      <Button
        data-cy="about-nav"
        onClick={() => {
          navigateTo("/about");
        }}>
        {t("about")}
      </Button>
    </FlexRowCenterBox>
  );
};

export default Footer;
