import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { User } from "./AuthInterfaces";
import { useAuth } from "react-oidc-context";

export const UserContext = createContext<User | undefined>(undefined);

export const UserProvider: FC<PropsWithChildren> = ({ children }) => {
  const [user, setUser] = useState<User>();
  const auth = useAuth();

  const fetchUserInfo = useCallback(async () => {
    const userResult = await fetch("/api/v1/user/self", {
      headers: {
        Authorization: `Bearer ${auth.user?.id_token}`,
      },
    });
    if (!userResult.ok) throw new Error(userResult.statusText);

    const user = ((await userResult.json()) as User) ?? undefined;
    setUser(user);
  }, [auth?.user?.id_token]);

  useEffect(() => {
    if (auth?.isAuthenticated) {
      fetchUserInfo();
    } else if (!auth?.isLoading) {
      setUser(undefined);
    }
  }, [auth?.isAuthenticated, auth?.isLoading, fetchUserInfo]);

  return <UserContext.Provider value={user}>{children}</UserContext.Provider>;
};
