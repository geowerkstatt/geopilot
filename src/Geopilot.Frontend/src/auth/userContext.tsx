import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { ApiError, User } from "../api/apiInterfaces";
import { useAuth } from "react-oidc-context";
import { useApi } from "../api";

export const UserContext = createContext<User | null | undefined>(undefined);

export const UserProvider: FC<PropsWithChildren> = ({ children }) => {
  const [user, setUser] = useState<User | null>();
  const auth = useAuth();
  const { fetchApi } = useApi();

  const fetchUserInfo = useCallback(async () => {
    fetchApi<User>("/api/v1/user/self", {
      headers: {
        Authorization: `Bearer ${auth.user?.id_token}`,
      },
      errorMessageLabel: "userLoadingError",
    })
      .then(setUser)
      .catch(error => {
        if (error instanceof ApiError && error.status === 401) {
          auth.removeUser();
          setUser(null);
        }
      });
  }, [auth, fetchApi]);

  useEffect(() => {
    if (auth?.isAuthenticated) {
      fetchUserInfo();
    } else if (auth && !auth.isLoading) {
      setUser(null);
    }
  }, [auth, auth?.isAuthenticated, auth?.isLoading, fetchUserInfo]);

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
