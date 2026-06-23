import { useContext } from "react";
import { ApiAuthConfigurationContext } from "./apiAuthConfigurationContext";
import { GeopilotAuthContext } from "./geopilotAuthComponent";
import { UserContext } from "./userContext";

export const useGeopilotAuth = () => useContext(GeopilotAuthContext);
export const useUser = () => useContext(UserContext);
export const useApiAuthConfiguration = () => useContext(ApiAuthConfigurationContext);
