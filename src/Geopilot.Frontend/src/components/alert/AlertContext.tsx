import { createContext, FC, useState } from "react";
import { AlertOptions, AlertProviderProps, AlertContextInterface } from "./AlertInterfaces";
import { AlertColor } from "@mui/material";

export const AlertContext = createContext<AlertContextInterface>({
  alertIsOpen: false,
  text: undefined,
  severity: undefined,
  autoHideDuration: null,
  showAlert: () => {},
  closeAlert: () => {},
});

export const AlertProvider: FC<AlertProviderProps> = ({ children }) => {
  const [alert, setAlert] = useState<AlertOptions | null>(null);

  const showAlert = (text: string, severity: AlertColor | undefined, allowAutoHide: boolean | undefined) => {
    setAlert({
      text: text,
      severity: severity ?? "info",
      allowAutoHide: allowAutoHide ?? false,
    });
  };

  const closeAlert = () => {
    setAlert(null);
  };

  return (
    <AlertContext.Provider
      value={{
        alertIsOpen: alert?.text != null,
        text: alert?.text,
        severity: alert?.severity,
        autoHideDuration: alert?.allowAutoHide === true ? 6000 : null,
        showAlert,
        closeAlert,
      }}>
      {children}
    </AlertContext.Provider>
  );
};
