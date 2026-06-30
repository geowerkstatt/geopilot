import { FC, PropsWithChildren } from "react";
import { useAuth } from "react-oidc-context";
import { useApiAuthConfiguration, useUser } from ".";
import { ApiAuthConfigurationProvider } from "./apiAuthConfigurationProvider";
import { CookieSynchronizer } from "./cookieSynchronizer";
import { GeopilotAuthContext } from "./geopilotAuthContext";
import { OidcContainerProvider } from "./oidcContainerContext";
import { UserProvider } from "./userProvider";

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
