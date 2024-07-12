import { createContext, FC, useState, useEffect, PropsWithChildren } from "react";
import { AuthSettings } from "./AuthInterfaces";

export const ApiAuthConfigurationContext = createContext<AuthSettings | undefined>(undefined);

const loadAuthConfiguration = async (): Promise<AuthSettings> => {
  const authConfigResult = await fetch("/api/v1/user/auth");
  if (!authConfigResult.ok || !authConfigResult.headers.get("content-type")?.includes("application/json")) {
    throw new Error();
  }

  return (await authConfigResult.json()) as AuthSettings;
};

export const ApiAuthConfigurationProvider: FC<PropsWithChildren> = ({ children }) => {
  const [apiAuthSettings, setApiAuthSettings] = useState<AuthSettings>();

  useEffect(() => {
    if (apiAuthSettings) return;

    const load = () => loadAuthConfiguration().then(setApiAuthSettings);
    load();

    // Retry every 3s
    const interval = setInterval(load, 3_000);

    // Cancel retry after 30s
    setTimeout(() => clearInterval(interval), 30_000);

    // Clear retry on successful load
    return () => {
      clearInterval(interval);
    };
  }, [apiAuthSettings]);

  return (
    <ApiAuthConfigurationContext.Provider value={apiAuthSettings}>{children}</ApiAuthConfigurationContext.Provider>
  );
};
