import { createContext, FC, PropsWithChildren, useEffect, useState } from "react";
import { AppSettingsContextInterface, ClientSettings } from "./appSettingsInterface";
import { useApi } from "../../api";
import { ContentType } from "../../api/apiInterfaces.ts";
import { useTranslation } from "react-i18next";

export const AppSettingsContext = createContext<AppSettingsContextInterface>({
  initialized: false,
  clientSettings: undefined,
  termsOfUse: undefined,
});

export const AppSettingsProvider: FC<PropsWithChildren> = ({ children }) => {
  const { i18n } = useTranslation();
  const { fetchApi } = useApi();
  const [clientSettings, setClientSettings] = useState<ClientSettings | null>();
  const [termsOfUse, setTermsOfUse] = useState<string | null>();

  useEffect(() => {
    fetchApi<ClientSettings>("/client-settings.json", { responseType: ContentType.Json })
      .then(setClientSettings)
      .catch(() => setClientSettings(null));

    fetchApi<string>(`/terms-of-use.${i18n.language}.md`, { responseType: ContentType.Markdown })
      .then(response => {
        if (response) {
          setTermsOfUse(response);
        } else {
          throw new Error("Language-specific terms of use not found");
        }
      })
      .catch(() => {
        fetchApi<string>("/terms-of-use.md", { responseType: ContentType.Markdown }).then(setTermsOfUse);
      });
  }, [fetchApi, i18n.language]);

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
    document.title = "geopilot " + clientSettings?.application?.name;
  }, [clientSettings?.application?.name]);

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
