import { useMsal } from "@azure/msal-react";
import { useCallback } from "react";

export function useAuthenticatedFetch(clientSettings) {
  const { instance } = useMsal();

  return useCallback(
    async function authenticatedFetch(url, options) {
      const authResult = await instance.acquireTokenSilent({
        scopes: clientSettings?.authScopes ?? [],
        account: instance.getActiveAccount(),
      });

      return await fetch(url, {
        ...options,
        headers: {
          ...options?.headers,
          Authorization: `Bearer ${authResult.idToken}`,
        },
      });
    },
    [instance, clientSettings],
  );
}
