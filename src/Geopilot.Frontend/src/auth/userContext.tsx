import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { User } from "../api/apiInterfaces";
import { useAuth } from "react-oidc-context";
import { useApi } from "../api";

export const UserContext = createContext<User | undefined>(undefined);

export const UserProvider: FC<PropsWithChildren> = ({ children }) => {
  const [user, setUser] = useState<User>();
  const auth = useAuth();
  const { fetchApi } = useApi();

  const fetchUserInfo = useCallback(async () => {
    fetchApi<User>("/api/v1/user/self", {
      headers: {
        Authorization: `Bearer ${auth.user?.id_token}`,
      },
      errorMessageLabel: "userLoadingError",
    }).then(setUser);
  }, [auth?.user?.id_token, fetchApi]);

  useEffect(() => {
    if (auth?.isAuthenticated) {
      fetchUserInfo();
    } else if (!auth?.isLoading) {
      setUser(undefined);
    }
  }, [auth?.isAuthenticated, auth?.isLoading, fetchUserInfo]);

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
