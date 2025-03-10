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
    logo: string;
    favicon: string;
    faviconDark?: string;
  };
}

export interface AppSettingsContextInterface {
  initialized: boolean;
  clientSettings?: ClientSettings | null;
}

export const useAppSettings = () => useContext(AppSettingsContext);
