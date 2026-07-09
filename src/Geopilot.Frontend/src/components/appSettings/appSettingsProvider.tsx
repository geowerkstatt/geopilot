import { FC, PropsWithChildren, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ContentType } from "../../api/apiInterfaces.ts";
import { resolveApplicationName } from "../../hooks/useApplicationName.ts";
import useFetch from "../../hooks/useFetch.ts";
import { AppSettingsContext } from "./appSettingsContext";
import { ClientSettings } from "./appSettingsInterface";

export const AppSettingsProvider: FC<PropsWithChildren> = ({ children }) => {
  const { i18n } = useTranslation();
  const { fetchApi, fetchLocalizedMarkdown } = useFetch();
  const [clientSettings, setClientSettings] = useState<ClientSettings | null>();
  const [termsOfUse, setTermsOfUse] = useState<string | null>();

  useEffect(() => {
    fetchApi<ClientSettings>("/client-settings.json", { responseType: ContentType.Json })
      .then(setClientSettings)
      .catch(() => setClientSettings(null));
  }, [fetchApi]);

  useEffect(() => {
    fetchLocalizedMarkdown("terms-of-use", i18n.language).then(setTermsOfUse);
  }, [fetchLocalizedMarkdown, i18n.language]);

  useEffect(() => {
    if (clientSettings) {
      const link = document.querySelector("link[rel=icon]");
      const defaultLink = document.querySelector("link[rel=icon]:not([data-dark-mode])");
      const darkModeLink = document.querySelector("link[rel=icon][data-dark-mode]");
      const faviconHref = clientSettings?.application?.favicon;
      const darkModeFaviconHref = clientSettings?.application?.faviconDark;

      if (faviconHref) {
        link?.setAttribute("href", faviconHref);
        defaultLink?.setAttribute("href", faviconHref);
      }

      if (darkModeFaviconHref) {
        if (darkModeLink) {
          darkModeLink.setAttribute("href", darkModeFaviconHref);
        } else {
          const newDarkModeLink = document.createElement("link");
          newDarkModeLink.setAttribute("rel", "icon");
          newDarkModeLink.setAttribute("href", darkModeFaviconHref);
          newDarkModeLink.setAttribute("media", "(prefers-color-scheme: dark)");
          newDarkModeLink.setAttribute("data-dark-mode", "true");
          document.head.appendChild(newDarkModeLink);
        }
      } else if (darkModeLink) {
        darkModeLink.remove();
      }
    }
  }, [clientSettings]);

  useEffect(() => {
    const applicationName = resolveApplicationName(clientSettings?.application, i18n.language);
    document.title = applicationName ? `geopilot ${applicationName}` : "geopilot";
  }, [clientSettings?.application, i18n.language]);

  return (
    <AppSettingsContext.Provider
      value={{
        initialized: clientSettings !== undefined && termsOfUse !== undefined,
        termsOfUse: termsOfUse,
        clientSettings: clientSettings,
      }}>
      {children}
    </AppSettingsContext.Provider>
  );
};
