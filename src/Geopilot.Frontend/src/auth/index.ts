import { useContext } from "react";
import { UserContext } from "./userContext";
import { ApiAuthConfigurationContext } from "./apiAuthConfigurationContext";
import { GeopilotAuthContext } from "./geopilotAuthComponent";

export const useGeopilotAuth = () => useContext(GeopilotAuthContext);
export const useUser = () => useContext(UserContext);
export const useApiAuthConfiguration = () => useContext(ApiAuthConfigurationContext);
