import { Trans, useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { Link, Typography } from "@mui/material";
import { MarkdownContent } from "../../components/markdownContent.tsx";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { ContentType } from "../../api/apiInterfaces.ts";
import { CenteredBox } from "../../components/styledComponents.ts";
import { useLocation } from "react-router-dom";

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
  const [version, setVersion] = useState<string | null>();
  const { fetchApi } = useApi();
  const { termsOfUse } = useAppSettings();
  const { hash } = useLocation();

  useEffect(() => {
    fetchApi<string>("/info.md", { responseType: ContentType.Markdown }).then(setInfo);
    fetchApi<PackageList>("/license.json", { responseType: ContentType.Json }).then(setLicenseInfo);
    fetchApi<PackageList>("/license.custom.json", { responseType: ContentType.Json }).then(setLicenseInfoCustom);
    fetchApi<string>("/api/v1/version").then(version => {
      setVersion(version.split("+")[0]);
    });
  }, [fetchApi]);

  useEffect(() => {
    const scrollToHash = () => {
      if (hash) {
        const id = hash.substring(1);
        const element = document.getElementById(id);
        if (element) window.scrollTo({ top: element.offsetTop - 64, behavior: "smooth" });
      }
    };

    // Run after initial render
    setTimeout(scrollToHash, 0);

    scrollToHash();
  }, [hash, info, termsOfUse, licenseInfo, licenseInfoCustom, version]);

  return (
    <CenteredBox>
      {info && <MarkdownContent content={info} routeHash={"info"} />}
      {termsOfUse && <MarkdownContent content={termsOfUse} routeHash={"termsofuse"} />}
      <Typography variant="h1" id="version">
        {t("api")}
      </Typography>
      <p>
        <Trans
          i18nKey="apiInformation"
          components={{
            apiLink: <Link href={`${window.location.origin}/swagger/index.html`} target="_blank" rel="noreferrer" />,
          }}
          values={{ version }}
        />
      </p>
      <Typography variant="h1" id="development">
        {t("development")} & {t("bugTracking")}
      </Typography>
      <p>
        <Trans
          i18nKey="codeLicenseInfo"
          components={{
            licenseLink: (
              <Link
                href="https://github.com/GeoWerkstatt/geopilot/blob/main/LICENSE"
                target="_blank"
                rel="noreferrer"
              />
            ),
            repositoryLink: <Link href="https://github.com/GeoWerkstatt/geopilot" target="_blank" rel="noreferrer" />,
            issuesLink: (
              <Link href="https://github.com/GeoWerkstatt/geopilot/issues/" target="_blank" rel="noreferrer" />
            ),
          }}
        />
      </p>
      {(licenseInfo || licenseInfoCustom) && (
        <Typography variant="h1" id="licenses">
          {t("licenseInformation")}
        </Typography>
      )}
      {licenseInfoCustom &&
        Object.keys(licenseInfoCustom).map(key => (
          <div key={key} className="about-licenses">
            <Typography variant="h3">
              {licenseInfoCustom[key].name}
              {licenseInfoCustom[key].version && ` (${t("version")} ${licenseInfoCustom[key].version})`}{" "}
            </Typography>
            <p>
              <Link href={licenseInfoCustom[key].repository}>{licenseInfoCustom[key].repository}</Link>
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
              <Link href={licenseInfo[key].repository}>{licenseInfo[key].repository}</Link>
            </p>
            <p>{licenseInfo[key].description}</p>
            <p>{licenseInfo[key].copyright}</p>
            <p>
              {t("licenses")}: {licenseInfo[key].licenses}
            </p>
            <p>{licenseInfo[key].licenseText}</p>
          </div>
        ))}
    </CenteredBox>
  );
};
