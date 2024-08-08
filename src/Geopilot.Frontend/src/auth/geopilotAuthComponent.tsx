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
  isAdmin: false,
  isLoggedIn: false,
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

  const enabled = auth !== undefined && !auth.isLoading;

  return (
    <GeopilotAuthContext.Provider
      value={{
        enabled: enabled,
        user: user,
        isAdmin: enabled && !!user?.isAdmin,
        isLoggedIn: enabled && !!user,
        login: auth !== undefined ? auth.signinPopup : () => {},
        logout: auth !== undefined ? auth.signoutRedirect : () => {},
      }}>
      {children}
    </GeopilotAuthContext.Provider>
  );
};
