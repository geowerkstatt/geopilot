import { createContext, FC, PropsWithChildren } from "react";
import { ApiAuthConfigurationProvider } from "./ApiAuthConfigurationContext";
import { GeopilotAuthComponentProps, IGeopilotAuthContext } from "./AuthInterfaces";
import { OidcContainerProvider } from "./OidcContainerContext";
import { UserProvider } from "./UserContext";
import { useUser } from ".";
import { useAuth } from "react-oidc-context";
import { CookieSynchronizer } from "./CookieSynchronizer";

export const GeopilotAuthContext = createContext<IGeopilotAuthContext>({
  enabled: false,
  user: undefined,
  login: () => {
    throw new Error();
  },
  logout: () => {
    throw new Error();
  },
});

export const GeopilotAuthProvider: FC<GeopilotAuthComponentProps> = ({ authScopes, children }) => {
  return (
    <ApiAuthConfigurationProvider>
      <OidcContainerProvider authScopes={authScopes}>
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
        enabled: auth !== undefined,
        user: user,
        login: auth !== undefined ? auth.signinPopup : () => {},
        logout: auth !== undefined ? auth.signoutRedirect : () => {},
      }}>
      {children}
    </GeopilotAuthContext.Provider>
  );
};
