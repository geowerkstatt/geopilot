import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { AlertContextInterface, AlertOptions } from "./alertInterfaces";
import { AlertColor } from "@mui/material";

export const AlertContext = createContext<AlertContextInterface>({
  alertIsOpen: false,
  text: undefined,
  severity: undefined,
  autoHideDuration: null,
  showAlert: () => {},
  closeAlert: () => {},
});

export const AlertProvider: FC<PropsWithChildren> = ({ children }) => {
  const [currentAlert, setCurrentAlert] = useState<AlertOptions>();
  const [alerts, setAlerts] = useState<AlertOptions[]>([]);

  const showAlert = useCallback(
    (text: string, severity: AlertColor | undefined, allowAutoHide: boolean | undefined) => {
      const newAlert = { text, severity: severity ?? "info", allowAutoHide: allowAutoHide ?? false };
      setAlerts(prevAlerts => [...prevAlerts, newAlert]);
    },
    [],
  );

  const closeAlert = () => {
    setCurrentAlert(undefined);
  };

  useEffect(() => {
    if (alerts.length > 0 && !currentAlert) {
      setCurrentAlert(alerts[0]);
      setAlerts(prevAlerts => prevAlerts.slice(1));
    }
  }, [alerts, currentAlert]);

  return (
    <AlertContext.Provider
      value={{
        alertIsOpen: currentAlert?.text != null,
        text: currentAlert?.text,
        severity: currentAlert?.severity,
        autoHideDuration: currentAlert?.allowAutoHide === true ? 6000 : null,
        showAlert,
        closeAlert,
      }}>
      {children}
    </AlertContext.Provider>
  );
};
