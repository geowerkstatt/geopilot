import InfoCarousel from "./InfoCarousel";
import "./app.css";

export const Title = ({ clientSettings, customAppLogoPresent, setCustomAppLogoPresent, quickStartContent }) => (
  <div className="title-wrapper">
    <div className="app-subtitle">Online Validierung von INTERLIS Daten</div>
    <div>
      <img
        className="app-logo"
        src="/app.svg"
        alt="App Logo"
        onLoad={() => setCustomAppLogoPresent(true)}
        onError={(e) => (e.target.style.display = "none")}
      />
    </div>
    {!customAppLogoPresent && <div className="app-title">{clientSettings?.applicationName}</div>}
    {quickStartContent && <InfoCarousel content={quickStartContent} />}
  </div>
);

export default Title;
