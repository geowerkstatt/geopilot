import { createContext, FC, PropsWithChildren } from "react";
import { ApiAuthConfigurationProvider } from "./apiAuthConfigurationContext";
import { GeopilotAuthContextInterface } from "./authInterfaces";
import { OidcContainerProvider } from "./oidcContainerContext";
import { UserProvider } from "./userContext";
import { useApiAuthConfiguration, useUser } from ".";
import { useAuth } from "react-oidc-context";
import { CookieSynchronizer } from "./cookieSynchronizer";

export const GeopilotAuthContext = createContext<GeopilotAuthContextInterface>({
  authLoaded: false,
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

  const authLoaded = !!(apiSetting && apiSetting.clientAudience && apiSetting.authority);
  const isLoading = !((!!apiSetting && !authLoaded) || user !== undefined);

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
        authLoaded: authLoaded,
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
