import { createContext, FC, PropsWithChildren, useEffect, useState } from "react";
import { AuthSettings } from "./authInterfaces";
import { useApi } from "../api";

export const ApiAuthConfigurationContext = createContext<AuthSettings | undefined>(undefined);

export const ApiAuthConfigurationProvider: FC<PropsWithChildren> = ({ children }) => {
  const [apiAuthSettings, setApiAuthSettings] = useState<AuthSettings>();
  const { fetchApi } = useApi();

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const loadAuthSettings = () => {
    fetchApi<AuthSettings>("/api/v1/user/auth").then(setApiAuthSettings);
  };

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
