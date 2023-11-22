import { useState } from "react";
import InfoCarousel from "./InfoCarousel";
import "./app.css";
import { useEffect } from "react";

export const Title = ({ clientSettings, quickStartContent }) => {
  const [customAppLogoPresent, setCustomAppLogoPresent] = useState(false);

  useEffect(() => {
    setCustomAppLogoPresent(clientSettings?.application?.logo !== undefined);
  }, [clientSettings?.application]);

  return (
    <div className="title-wrapper">
      <div className="app-subtitle">
        Online Validierung & Abgabe von Geodaten
      </div>
      {customAppLogoPresent ? (
        <div>
          <img
            className="app-logo"
            src={clientSettings?.application?.logo}
            alt="App Logo"
            onError={() => setCustomAppLogoPresent(false)}
          />
        </div>
      ) : (
        <div className="app-title">{clientSettings?.application?.name}</div>
      )}
      {quickStartContent && <InfoCarousel content={quickStartContent} />}
    </div>
  );
};

export default Title;
