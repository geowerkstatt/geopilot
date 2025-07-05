import { FC, PropsWithChildren, useMemo } from "react";
import { User, WebStorageStateStore } from "oidc-client-ts";
import { useApiAuthConfiguration } from ".";
import { AuthProvider, AuthProviderProps } from "react-oidc-context";
import { useAppSettings } from "../components/appSettings/appSettingsInterface";

// eslint-disable-next-line @typescript-eslint/no-unused-vars
const onSigninCallback = (user: User | void) => {
  window.history.replaceState({}, document.title, window.location.pathname);
};

export const OidcContainerProvider: FC<PropsWithChildren> = ({ children }) => {
  const { clientSettings } = useAppSettings();
  const apiSetting = useApiAuthConfiguration();

  const scopes = useMemo(() => {
    if (!clientSettings?.authScopes) return "";

    const scopeList = [...clientSettings.authScopes];
    if (apiSetting?.organizationId) {
      scopeList.push(`urn:zitadel:iam:org:id:${apiSetting.organizationId}`);
    }
    return scopeList.join(" ");
  }, [clientSettings?.authScopes, apiSetting?.organizationId]);

  if (!clientSettings?.authScopes) return children;
  if (!apiSetting) return children;

  const oidcConfig: AuthProviderProps = {
    authority: apiSetting.authority,
    client_id: apiSetting.clientId,
    scope: scopes,
    redirect_uri: window.location.origin,
    post_logout_redirect_uri: window.location.origin,
    onSigninCallback: onSigninCallback,
    userStore: new WebStorageStateStore({ store: window.localStorage }),
  };

  return <AuthProvider {...oidcConfig}>{children}</AuthProvider>;
};
