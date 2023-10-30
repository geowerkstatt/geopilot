import { useState } from "react";
import InfoCarousel from "./InfoCarousel";
import "./app.css";

export const Title = ({ clientSettings, quickStartContent }) => {
  const [customAppLogoPresent, setCustomAppLogoPresent] = useState(false);

  return (
    <div className="title-wrapper">
      <div className="app-subtitle">Online Validierung von INTERLIS Daten</div>
      <div>
        <img
          className="app-logo"
          src={`/${clientSettings?.application?.logo}`}
          alt="App Logo"
          onLoad={() => setCustomAppLogoPresent(true)}
          onError={(e) => (e.target.style.display = "none")}
        />
      </div>
      {!customAppLogoPresent && <div className="app-title">{clientSettings?.application?.name}</div>}
      {quickStartContent && <InfoCarousel content={quickStartContent} />}
    </div>
  );
};

export default Title;
