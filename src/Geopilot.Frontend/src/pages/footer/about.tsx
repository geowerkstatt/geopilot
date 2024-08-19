import { Trans, useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { Box, Typography } from "@mui/material";
import { MarkdownContent } from "./markdownContent.tsx";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { ContentType } from "../../api/apiInterfaces.ts";

interface PackageList {
  [packageName: string]: PackageDetails;
}

interface PackageDetails {
  licenses?: string;
  repository?: string;
  publisher?: string;
  email?: string;
  url?: string;
  name: string;
  version: string;
  description?: string;
  copyright?: string;
  licenseText?: string;
  path?: string;
  licenseFile?: string;
}

export const About = () => {
  const { t } = useTranslation();
  const [info, setInfo] = useState<string>();
  const [licenseInfo, setLicenseInfo] = useState<PackageList>();
  const [licenseInfoCustom, setLicenseInfoCustom] = useState<PackageList>();
  const { fetchApi } = useApi();
  const { version, clientSettings, termsOfUse } = useAppSettings();

  useEffect(() => {
    fetchApi<string>("info.md", { responseType: ContentType.Markdown }).then(setInfo);
    fetchApi<PackageList>("licenses.json", { responseType: ContentType.Json }).then(setLicenseInfo);
    fetchApi<PackageList>("licenses.custom.json", { responseType: ContentType.Json }).then(setLicenseInfoCustom);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Box sx={{ maxWidth: "1000px" }}>
      {info && <MarkdownContent content={info} />}
      {termsOfUse && <MarkdownContent content={termsOfUse} />}
      <Typography variant="h1">{t("versionInformation")}</Typography>
      <p>
        <b>geopilot {clientSettings?.application?.name}</b>: {version}
      </p>
      <Typography variant="h1">
        {t("development")} & {t("bugTracking")}
      </Typography>
      <p>
        <Trans
          i18nKey="codeLicenseInfo"
          components={{
            licenseLink: (
              <a href="https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE" target="_blank" rel="noreferrer" />
            ),
            repositoryLink: <a href="https://github.com/GeoWerkstatt/geopilot" target="_blank" rel="noreferrer" />,
            issuesLink: <a href="https://github.com/GeoWerkstatt/geopilot/issues/" target="_blank" rel="noreferrer" />,
          }}
        />
      </p>
      {(licenseInfo || licenseInfoCustom) && <Typography variant="h1">{t("licenseInformation")}</Typography>}
      {licenseInfoCustom &&
        Object.keys(licenseInfoCustom).map(key => (
          <div key={key} className="about-licenses">
            <Typography variant="h3">
              {licenseInfoCustom[key].name}
              {licenseInfoCustom[key].version && ` (${t("version")} ${licenseInfoCustom[key].version})`}{" "}
            </Typography>
            <p>
              <a href={licenseInfoCustom[key].repository}>{licenseInfoCustom[key].repository}</a>
            </p>
            <p>{licenseInfoCustom[key].description}</p>
            <p>{licenseInfoCustom[key].copyright}</p>
            <p>
              {t("licenses")}: {licenseInfoCustom[key].licenses}
            </p>
            <p>{licenseInfoCustom[key].licenseText}</p>
          </div>
        ))}
      {licenseInfo &&
        Object.keys(licenseInfo).map(key => (
          <div key={key} className="about-licenses">
            <Typography variant="h3">
              {licenseInfo[key].name}
              {licenseInfo[key].version && ` (${t("version")} ${licenseInfo[key].version})`}{" "}
            </Typography>
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
    </Box>
  );
};
