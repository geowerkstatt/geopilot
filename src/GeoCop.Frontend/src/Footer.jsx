import "./app.css";
import About from "./About";
import swissMadeSwissHosted from "./assets/sms-sh.png";
import { Button } from "react-bootstrap";

export const Footer = (props) => {
  const {
    openModalContent,
    infoHilfeContent,
    nutzungsbestimmungenContent,
    datenschutzContent,
    impressumContent,
    clientSettings,
    licenseInfoCustom,
    licenseInfo,
  } = props;

  return (
    <footer className="footer-style">
      <div>
        {infoHilfeContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(infoHilfeContent, "markdown")}
          >
            INFO & HILFE
          </Button>
        )}
        {nutzungsbestimmungenContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(nutzungsbestimmungenContent, "markdown")}
          >
            NUTZUNGSBESTIMMUNGEN
          </Button>
        )}
        {datenschutzContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(datenschutzContent, "markdown")}
          >
            DATENSCHUTZ
          </Button>
        )}
        {impressumContent && (
          <Button
            variant="link"
            className="footer-button no-outline-on-focus"
            onClick={() => openModalContent(impressumContent, "markdown")}
          >
            IMPRESSUM
          </Button>
        )}
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() =>
            openModalContent(
              <About clientSettings={clientSettings} licenseInfo={{ ...licenseInfoCustom, ...licenseInfo }} />,
              "raw",
            )
          }
        >
          ABOUT
        </Button>
      </div>
      <div className="footer-icons">
        <a
          href="https://www.swissmadesoftware.org/en/home/swiss-hosting.html"
          title="Link zu Swiss Hosting"
          target="_blank"
          rel="noreferrer"
        >
          <img className="footer-icon" src={swissMadeSwissHosted} alt="Swiss Hosting Logo" />
        </a>
      </div>
    </footer>
  );
};

export default Footer;
