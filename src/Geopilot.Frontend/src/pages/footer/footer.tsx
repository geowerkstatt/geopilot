import { Button } from "@mui/material";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { FlexRowCenterBox } from "../../components/styledComponents.ts";

const Footer = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();

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
    </FlexRowCenterBox>
  );
};

export default Footer;
