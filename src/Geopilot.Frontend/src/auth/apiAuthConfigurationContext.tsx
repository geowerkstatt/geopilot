import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { AuthSettings } from "./authInterfaces";
import { useApi } from "../api";

export const ApiAuthConfigurationContext = createContext<AuthSettings | undefined>(undefined);

export const ApiAuthConfigurationProvider: FC<PropsWithChildren> = ({ children }) => {
  const [apiAuthSettings, setApiAuthSettings] = useState<AuthSettings>();
  const { fetchApi } = useApi();

  const loadAuthSettings = useCallback(() => {
    fetchApi<AuthSettings>("/api/v1/user/auth").then(setApiAuthSettings);
  }, [fetchApi]);

  useEffect(() => {
    if (apiAuthSettings) return;

    loadAuthSettings();
    // Retry every 3s
    const interval = setInterval(loadAuthSettings, 3_000);

    // Cancel retry after 30s
    setTimeout(() => clearInterval(interval), 30_000);

    // Clear retry on successful load
    return () => {
      clearInterval(interval);
    };
  }, [apiAuthSettings, loadAuthSettings]);

  return (
    <ApiAuthConfigurationContext.Provider value={apiAuthSettings}>{children}</ApiAuthConfigurationContext.Provider>
  );
};
