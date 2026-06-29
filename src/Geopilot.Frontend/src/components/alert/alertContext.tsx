import { createContext } from "react";
import { AlertContextInterface } from "./alertInterfaces";

export const AlertContext = createContext<AlertContextInterface>({
  alertIsOpen: false,
  text: undefined,
  severity: undefined,
  autoHideDuration: null,
  showAlert: () => {},
  closeAlert: () => {},
});
