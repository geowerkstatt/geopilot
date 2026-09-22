import { createContext } from "react";
import { CapabilitiesContextInterface } from "./capabilitiesInterface";

export const CapabilitiesContext = createContext<CapabilitiesContextInterface>({
  machineDeliveryEnabled: false,
});
