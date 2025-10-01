import { useContext } from "react";
import { AppSettingsContext } from "./appSettingsContext";

export interface ClientSettings {
  authCache: {
    cacheLocation: string;
    storeAuthStateInCookie: boolean;
  };
  application: {
    name?: string;
    localName?: {
      [languageCode: string]: string;
    };
    logo: string;
    favicon: string;
    faviconDark?: string;
  };
}

export interface AppSettingsContextInterface {
  initialized: boolean;
  clientSettings?: ClientSettings | null;
  termsOfUse?: string | null;
}

export const useAppSettings = () => useContext(AppSettingsContext);
