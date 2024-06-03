import "./app.css";
import About from "./About";
import swissMadeSwissHosted from "./assets/sms-sh.png";
import { Button } from "react-bootstrap";
import { useTranslation } from "react-i18next";

export const Footer = ({
  openModalContent,
  infoHilfeContent,
  nutzungsbestimmungenContent,
  datenschutzContent,
  impressumContent,
  clientSettings,
  licenseInfoCustom,
  licenseInfo,
  appVersion,
}) => {
  const { t } = useTranslation();
  return (
    <footer className="footer-style">
      <div>
        {infoHilfeContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(infoHilfeContent, "markdown")}>
            {t("info").toUpperCase()} & {t("help").toUpperCase()}
          </Button>
        )}
        {nutzungsbestimmungenContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(nutzungsbestimmungenContent, "markdown")}>
            {t("termsOfUse").toUpperCase()}
          </Button>
        )}
        {datenschutzContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(datenschutzContent, "markdown")}>
            {t("privacyPolicy").toUpperCase()}
          </Button>
        )}
        {impressumContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(impressumContent, "markdown")}>
            {t("impressum").toUpperCase()}
          </Button>
        )}
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() =>
            openModalContent(
              <About
                clientSettings={clientSettings}
                appVersion={appVersion}
                licenseInfo={{ ...licenseInfoCustom, ...licenseInfo }}
              />,
              "raw",
            )
          }>
          {t("about").toUpperCase()}
        </Button>
      </div>
      <div className="footer-icons">
        <a
          href="https://www.swissmadesoftware.org/en/home/swiss-hosting.html"
          title="Link zu Swiss Hosting"
          target="_blank"
          rel="noreferrer">
          <img className="footer-icon" src={swissMadeSwissHosted} alt="Swiss Hosting Logo" />
        </a>
      </div>
    </footer>
  );
};

export default Footer;
