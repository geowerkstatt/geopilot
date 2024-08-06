import { FC } from "react";
import { User, WebStorageStateStore } from "oidc-client-ts";
import { useApiAuthConfiguration } from ".";
import { AuthProvider, AuthProviderProps } from "react-oidc-context";
import { OidcContainerProps } from "./AuthInterfaces";

const onSigninCallback = (user: User | void) => {
  window.history.replaceState({}, document.title, window.location.pathname);
};

export const OidcContainerProvider: FC<OidcContainerProps> = ({ children, authScopes }) => {
  const apiSetting = useApiAuthConfiguration();

  if (!authScopes) return children;
  if (!apiSetting) return children;

  const oidcConfig: AuthProviderProps = {
    authority: apiSetting.authority,
    client_id: apiSetting.clientId,
    scope: authScopes.join(" "),
    redirect_uri: window.location.origin,
    post_logout_redirect_uri: window.location.origin,
    onSigninCallback: onSigninCallback,
    userStore: new WebStorageStateStore({ store: window.localStorage }),
  };

  return <AuthProvider {...oidcConfig}>{children}</AuthProvider>;
};
