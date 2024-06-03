import { useTranslation, Trans } from "react-i18next";

export const About = ({ clientSettings, licenseInfo, appVersion }) => {
  const { t } = useTranslation();

  return (
    <div>
      <h1>{t("about")}</h1>
      <h2>{t("versionInformation")}</h2>
      <p>
        <b>{clientSettings?.application?.name}</b>: {appVersion}
      </p>
      <h2>
        {t("development")} & {t("bugTracking")}
      </h2>
      <p>
        <Trans
          i18nKey="codeLicenseInfo"
          components={{
            licenseLink: <a href="https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE" target="_blank" />,
            repositoryLink: <a href="https://github.com/GeoWerkstatt/geopilot" target="_blank" />,
            issuesLink: <a href="https://github.com/GeoWerkstatt/geopilot/issues/" target="_blank" />,
          }}
        />
      </p>
      <h2>{t("licenseInformation")}</h2>
      {Object.keys(licenseInfo).map(key => (
        <div key={key} className="about-licenses">
          <h3>
            {licenseInfo[key].name}
            {licenseInfo[key].version && ` (${t("version")} ${licenseInfo[key].version})`}{" "}
          </h3>
          <p>
            <a href={licenseInfo[key].repository}>{licenseInfo[key].repository}</a>
          </p>
          <p>{licenseInfo[key].description}</p>
          <p>{licenseInfo[key].copyright}</p>
          <p>
            {t("licenses")}: {licenseInfo[key].licenses}
          </p>
          <p>{licenseInfo[key].licenseText}</p>
        </div>
      ))}
    </div>
  );
};

export default About;
