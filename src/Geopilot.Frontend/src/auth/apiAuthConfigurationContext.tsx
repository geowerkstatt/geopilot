import { createContext } from "react";
import { AuthSettings } from "./authInterfaces";

export const ApiAuthConfigurationContext = createContext<AuthSettings | undefined>(undefined);
