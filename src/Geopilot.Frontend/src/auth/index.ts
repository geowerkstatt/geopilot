import { useContext } from "react";
import { UserContext } from "./UserContext.js";
import { ApiAuthConfigurationContext } from "./ApiAuthConfigurationContext.js";
import { GeopilotAuthContext } from "./GeopilotAuthComponent.js";

export const useGeopilotAuth = () => useContext(GeopilotAuthContext);
export const useUser = () => useContext(UserContext);
export const useApiAuthConfiguration = () => useContext(ApiAuthConfigurationContext);
