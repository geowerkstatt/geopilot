import { createContext, FC, PropsWithChildren } from "react";
import { ApiAuthConfigurationProvider } from "./apiAuthConfigurationContext";
import { GeopilotAuthContextInterface } from "./authInterfaces";
import { OidcContainerProvider } from "./oidcContainerContext";
import { UserProvider } from "./userContext";
import { useUser } from ".";
import { useAuth } from "react-oidc-context";
import { CookieSynchronizer } from "./cookieSynchronizer";

export const GeopilotAuthContext = createContext<GeopilotAuthContextInterface>({
  enabled: false,
  user: undefined,
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

  return (
    <GeopilotAuthContext.Provider
      value={{
        enabled: auth !== undefined && !auth.isLoading,
        user: user,
        login: auth !== undefined ? auth.signinPopup : () => {},
        logout: auth !== undefined ? auth.signoutRedirect : () => {},
      }}>
      {children}
    </GeopilotAuthContext.Provider>
  );
};
