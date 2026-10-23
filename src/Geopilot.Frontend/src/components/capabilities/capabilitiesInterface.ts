import { useContext } from "react";
import { CapabilitiesContext } from "./capabilitiesContext";

export interface CapabilitiesContextInterface {
  /** Whether this installation offers machine based delivery. Configured on the server, never here. */
  machineDeliveryEnabled: boolean;
  /** Whether a mandate can choose a reaction set, which needs a delete window that closes. Configured on the server. */
  reactionSetsEnabled: boolean;
}

export const useCapabilities = () => useContext(CapabilitiesContext);
