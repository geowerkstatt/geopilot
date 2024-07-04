import { useContext } from "react";
import { AlertContext } from "./alertContext.jsx";
import { Alert, Snackbar } from "@mui/material";

export const AlertBanner = () => {
  const { alertIsOpen, text, severity, autoHideDuration, closeAlert } = useContext(AlertContext);
  return (
    alertIsOpen && (
      <Snackbar
        open={alertIsOpen}
        anchorOrigin={{ vertical: "top", horizontal: "center" }}
        autoHideDuration={autoHideDuration}
        onClose={closeAlert}>
        <Alert variant="filled" severity={severity} onClose={closeAlert}>
          {text}
        </Alert>
      </Snackbar>
    )
  );
};
