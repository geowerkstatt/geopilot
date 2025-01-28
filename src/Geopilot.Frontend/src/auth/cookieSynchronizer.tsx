import { FC, useEffect } from "react";
import { useAuth } from "react-oidc-context";

const setCookie = (id_token: string) => {
  document.cookie = `geopilot.auth=${id_token};Path=/;Secure`;
};

const clearCookie = () => {
  document.cookie = "geopilot.auth=;expires=Thu, 01 Jan 1970 00:00:00 GMT;Path=/;Secure";
};

export const CookieSynchronizer: FC = () => {
  const auth = useAuth();

  useEffect(() => {
    if (auth?.isAuthenticated && auth?.user?.id_token) {
      setCookie(auth.user.id_token);
    } else {
      clearCookie();
    }
  }, [auth, auth?.isAuthenticated, auth?.user?.id_token]);

  return <></>;
};
