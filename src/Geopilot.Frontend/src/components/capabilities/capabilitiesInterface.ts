import { useContext } from "react";
import { CapabilitiesContext } from "./capabilitiesContext";

export interface CapabilitiesContextInterface {
  /** Whether this installation offers machine based delivery. Configured on the server, never here. */
  machineDeliveryEnabled: boolean;
}

export const useCapabilities = () => useContext(CapabilitiesContext);
