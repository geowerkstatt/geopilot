import { FC, PropsWithChildren, useCallback, useEffect, useRef, useState } from "react";
import { AuthContextProps, useAuth } from "react-oidc-context";
import { ApiError } from "../api/apiInterfaces";
import { User } from "../api/generated";
import useFetch from "../hooks/useFetch.ts";
import { UserContext } from "./userContext";

export const UserProvider: FC<PropsWithChildren> = ({ children }) => {
  const [user, setUser] = useState<User | null>();
  const [attempt, setAttempt] = useState(0);
  const retryTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const auth = useAuth();
  const { fetchApi } = useFetch();

  const fetchUserInfo = useCallback(
    (auth: AuthContextProps) => {
      fetchApi<User>("/api/v1/user/self", {
        headers: {
          Authorization: `Bearer ${auth.user?.access_token}`,
        },
        errorMessageLabel: attempt === 0 ? "userInfoLoadingError" : undefined,
      })
        .then(user => {
          setUser(user);
          setAttempt(0);
        })
        .catch(error => {
          if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
            auth.signoutSilent();
            setUser(null);
            setAttempt(0);
          } else {
            retryTimer.current = setTimeout(() => setAttempt(current => current + 1), 5000);
          }
        });
    },
    [fetchApi, attempt],
  );

  useEffect(() => {
    if (auth?.isAuthenticated) {
      fetchUserInfo(auth);
    } else if (auth && !auth.isLoading) {
      setUser(null);
      setAttempt(0);
    }
    return () => clearTimeout(retryTimer.current);
  }, [auth, auth?.isAuthenticated, auth?.isLoading, fetchUserInfo]);

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
