import { Trans, useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { Link as RouterLink } from "react-router-dom";
import { Link, Typography } from "@mui/material";
import { MarkdownContent } from "../../components/markdownContent.tsx";
import { CenteredBox } from "../../components/styledComponents.ts";
import { useLocation } from "react-router-dom";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import useFetch from "../../hooks/useFetch.ts";

export const About = () => {
  const { t, i18n } = useTranslation();
  const [info, setInfo] = useState<string | null>();
  const [version, setVersion] = useState<string | null>();
  const { termsOfUse } = useAppSettings();
  const { fetchApi, fetchLocalizedMarkdown } = useFetch();
  const { hash } = useLocation();

  useEffect(() => {
    fetchLocalizedMarkdown("info", i18n.language).then(setInfo);
    fetchApi<string>("/api/v1/version").then(version => {
      setVersion(version.split("+")[0]);
    });
  }, [fetchApi, fetchLocalizedMarkdown, i18n.language]);

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
  }, [hash, info, termsOfUse, version]);

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
      <Typography variant="h1" id="licenses">
        {t("licenseInformation")}
      </Typography>
      <Typography variant="body1" id="licenses-text">
        <Trans
          i18nKey="licenseInformationDescription"
          components={{
            licenseLink: <Link component={RouterLink} to="/licenses" />,
          }}
        />
      </Typography>
    </CenteredBox>
  );
};
