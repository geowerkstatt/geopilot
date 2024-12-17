import { createContext, FC, PropsWithChildren } from "react";
import { useAuth } from "react-oidc-context";
import { useApiAuthConfiguration, useUser } from ".";
import { ApiAuthConfigurationProvider } from "./apiAuthConfigurationContext";
import { GeopilotAuthContextInterface } from "./authInterfaces";
import { CookieSynchronizer } from "./cookieSynchronizer";
import { OidcContainerProvider } from "./oidcContainerContext";
import { UserProvider } from "./userContext";

export const GeopilotAuthContext = createContext<GeopilotAuthContextInterface>({
  authEnabled: false,
  isLoading: false,
  user: undefined,
  isAdmin: false,
  login: () => {
    throw new Error();
  },
  logout: () => {
    throw new Error();
  },
});

export const GeopilotAuthProvider: FC<PropsWithChildren> = ({ children }) => {
  return (
    <ApiAuthConfigurationProvider>
      <OidcContainerProvider>
        <UserProvider>
          <GeopilotAuthContextMerger>{children}</GeopilotAuthContextMerger>
        </UserProvider>
        <CookieSynchronizer />
      </OidcContainerProvider>
    </ApiAuthConfigurationProvider>
  );
};

const GeopilotAuthContextMerger: FC<PropsWithChildren> = ({ children }) => {
  const auth = useAuth();
  const user = useUser();
  const apiSetting = useApiAuthConfiguration();

  const authEnabled = !!(apiSetting && apiSetting.clientId && apiSetting.authority);
  const isLoading = !((!!apiSetting && !authEnabled) || user !== undefined);

  const getLoginFunction = () => {
    if (!auth) return () => {};
    if (window.Cypress) {
      return auth.signinRedirect;
    } else {
      return auth.signinPopup;
    }
  };

  return (
    <GeopilotAuthContext.Provider
      value={{
        authEnabled: authEnabled,
        isLoading: isLoading,
        user: user,
        isAdmin: !!user?.isAdmin,
        login: getLoginFunction(),
        logout: auth !== undefined ? auth.signoutRedirect : () => {},
      }}>
      {children}
    </GeopilotAuthContext.Provider>
  );
};
