import { FC, PropsWithChildren } from "react";
import { User, WebStorageStateStore } from "oidc-client-ts";
import { useApiAuthConfiguration } from ".";
import { AuthProvider, AuthProviderProps } from "react-oidc-context";

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const onSigninCallback = (user: User | void) => {
  window.history.replaceState({}, document.title, window.location.pathname);
};

export const OidcContainerProvider: FC<PropsWithChildren> = ({ children }) => {
  const apiSetting = useApiAuthConfiguration();

  if (!apiSetting) return children;

  const oidcConfig: AuthProviderProps = {
    authority: apiSetting.authority,
    client_id: apiSetting.clientAudience,
    scope: apiSetting.fullScope,
    redirect_uri: window.location.origin,
    post_logout_redirect_uri: window.location.origin,
    onSigninCallback: onSigninCallback,
    userStore: new WebStorageStateStore({ store: window.localStorage }),
  };

  return <AuthProvider {...oidcConfig}>{children}</AuthProvider>;
};
