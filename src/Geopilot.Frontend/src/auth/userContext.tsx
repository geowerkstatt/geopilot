import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { User } from "../api/apiInterfaces";
import { useAuth } from "react-oidc-context";
import useFetch from "../hooks/useFetch.ts";

export const UserContext = createContext<User | null | undefined>(undefined);

export const UserProvider: FC<PropsWithChildren> = ({ children }) => {
  const [user, setUser] = useState<User | null>();
  const auth = useAuth();
  const { fetchApi } = useFetch();

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
    } else if (!!auth && !auth?.isLoading) {
      setUser(null);
    }
  }, [auth, auth?.isAuthenticated, auth?.isLoading, fetchUserInfo]);

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
