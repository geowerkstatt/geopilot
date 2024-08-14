import { createContext, FC, PropsWithChildren, useEffect, useState } from "react";
import { AppSettingsContextInterface, ClientSettings } from "./appSettingsInterface";
import { FetchContentType, runFetch } from "../../api/fetch";

export const AppSettingsContext = createContext<AppSettingsContextInterface>({
  version: undefined,
  clientSettings: undefined,
});

export const AppSettingsProvider: FC<PropsWithChildren> = ({ children }) => {
  const [clientSettings, setClientSettings] = useState<ClientSettings>();
  const [backendVersion, setBackendVersion] = useState("");

  useEffect(() => {
    runFetch({
      url: "client-settings.json",
      onSuccess: settings => {
        setClientSettings(settings as ClientSettings);
      },
    });
    runFetch({
      url: "/api/v1/version",
      contentType: FetchContentType.TEXT,
      onSuccess: version => {
        setBackendVersion((version as string).split("+")[0]);
      },
    });
  }, []);

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
        version: backendVersion,
        clientSettings: clientSettings,
      }}>
      {children}
    </AppSettingsContext.Provider>
  );
};
