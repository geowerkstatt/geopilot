import { createContext } from "react";
import { AppSettingsContextInterface } from "./appSettingsInterface";

export const AppSettingsContext = createContext<AppSettingsContextInterface>({
  initialized: false,
  clientSettings: undefined,
  termsOfUse: undefined,
});
