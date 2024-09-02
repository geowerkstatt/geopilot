import { createContext, FC, PropsWithChildren, useEffect, useState } from "react";
import { AppSettingsContextInterface, ClientSettings } from "./appSettingsInterface";
import { useApi } from "../../api";
import { ContentType } from "../../api/apiInterfaces.ts";

export const AppSettingsContext = createContext<AppSettingsContextInterface>({
  initialized: false,
  version: undefined,
  clientSettings: undefined,
  termsOfUse: undefined,
});

export const AppSettingsProvider: FC<PropsWithChildren> = ({ children }) => {
  const { fetchApi } = useApi();
  const [clientSettings, setClientSettings] = useState<ClientSettings | null>();
  const [backendVersion, setBackendVersion] = useState<string | null>();
  const [termsOfUse, setTermsOfUse] = useState<string | null>();

  useEffect(() => {
    fetchApi<ClientSettings>("client-settings.json")
      .then(setClientSettings)
      .catch(() => setClientSettings(null));
    fetchApi<string>("/api/v1/version")
      .then(version => {
        setBackendVersion(version.split("+")[0]);
      })
      .catch(() => setBackendVersion(null));
    fetchApi<string>("terms-of-use.md", { responseType: ContentType.Markdown })
      .then(setTermsOfUse)
      .catch(() => setTermsOfUse(null));
  }, [fetchApi]);

  useEffect(() => {
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
  }, [clientSettings]);

  useEffect(() => {
    document.title = "geopilot " + clientSettings?.application?.name + " " + backendVersion;
  }, [backendVersion, clientSettings?.application?.name]);

  return (
    <AppSettingsContext.Provider
      value={{
        initialized: clientSettings !== undefined && backendVersion !== undefined && termsOfUse !== undefined,
        version: backendVersion,
        clientSettings: clientSettings,
        termsOfUse: termsOfUse,
      }}>
      {children}
    </AppSettingsContext.Provider>
  );
};
