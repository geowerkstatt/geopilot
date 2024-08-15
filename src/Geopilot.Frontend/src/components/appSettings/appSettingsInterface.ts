import { useContext } from "react";
import { AppSettingsContext } from "./appSettingsContext";

export interface ClientSettings {
  authCache: {
    cacheLocation: string;
    storeAuthStateInCookie: boolean;
  };
  authScopes: string[];
  application: {
    name?: string;
    url: string;
    logo: string;
    favicon: string;
    faviconDark?: string;
  };
}

export interface AppSettingsContextInterface {
  version?: string;
  clientSettings?: ClientSettings;
  termsOfUse?: string;
}

export const useAppSettings = () => useContext(AppSettingsContext);
