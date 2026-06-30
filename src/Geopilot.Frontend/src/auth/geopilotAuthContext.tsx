import { createContext } from "react";
import { GeopilotAuthContextInterface } from "./authInterfaces";

export const GeopilotAuthContext = createContext<GeopilotAuthContextInterface>({
  authLoaded: false,
  isLoading: false,
  user: undefined,
  isAdmin: false,
  login: () => {
    throw new Error();
  },
  logout: () => {
    throw new Error();
  },
});
