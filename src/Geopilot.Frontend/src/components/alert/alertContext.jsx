import { createContext, useState } from "react";

export const AlertContext = createContext({
  alertIsOpen: false,
  text: "",
  severity: "",
  autoHideDuration: null,
  showAlert: () => {},
  closeAlert: () => {},
});

export const AlertProvider = props => {
  const [alert, setAlert] = useState(null);

  const showAlert = (text, severity, allowAutoHide) => {
    setAlert({
      text: text,
      severity: severity ? severity : "info",
      autoHideDuration: allowAutoHide === true ? 6000 : null,
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
        autoHideDuration: alert?.autoHideDuration,
        showAlert,
        closeAlert,
      }}>
      {props.children}
    </AlertContext.Provider>
  );
};
