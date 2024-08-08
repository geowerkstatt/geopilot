import { useState } from "react";
import InfoCarousel from "./InfoCarousel.jsx";
import "../../app.css";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

export const Title = ({ clientSettings, quickStartContent }) => {
  const { t } = useTranslation();
  const [customAppLogoPresent, setCustomAppLogoPresent] = useState(false);

  useEffect(() => {
    setCustomAppLogoPresent(clientSettings?.application?.logo !== undefined);
  }, [clientSettings?.application]);

  return (
    <div className="title-wrapper">
      <div className="app-subtitle">{t("appSubTitle")}</div>
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
