import { FC } from "react";
import { Trans, useTranslation } from "react-i18next";
import { Alert, Link } from "@mui/material";
import { useGeopilotAuth } from "../../auth";

export const DeliveryNoMandateAlert: FC = () => {
  const { t } = useTranslation();
  const { user, authLoaded, login } = useGeopilotAuth();

  return (
    <Alert
      variant="outlined"
      severity={user ? "warning" : "info"}
      data-cy="no-mandate-available"
      // Stacks above the delivery content's sticky overlay, which would otherwise paint over the alert.
      sx={{ position: "relative", zIndex: 11, transform: "translateZ(0)", backgroundColor: "background.content" }}>
      {user ? (
        t("noMandateAvailableSignedIn")
      ) : (
        <Trans
          i18nKey="noMandateAvailableAnonymous"
          components={{
            loginLink: authLoaded ? (
              <Link component="button" type="button" onClick={login} data-cy="no-mandate-login-link" />
            ) : (
              <span />
            ),
          }}
        />
      )}
    </Alert>
  );
};
