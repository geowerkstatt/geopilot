import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import PersonOutlinedIcon from "@mui/icons-material/PersonOutlined";
import SmartToyOutlinedIcon from "@mui/icons-material/SmartToyOutlined";
import { Link, Stack, Typography } from "@mui/material";
import { useCapabilities } from "./capabilities/capabilitiesInterface.ts";

interface UserOrClientLabelProps {
  /** A machine client when `machineClientId` is set, a user otherwise. */
  userOrClient?: { name: string; machineClientId?: number | null } | null;
  /** Where the name links to; without it the name is plain text. */
  path?: string;
}

/**
 * Who delivered or started a run, or "anonymous". Where machine delivery is offered, an icon tells a user
 * from a machine client.
 */
export const UserOrClientLabel: FC<UserOrClientLabelProps> = ({ userOrClient, path }) => {
  const { t } = useTranslation();
  const { machineDeliveryEnabled } = useCapabilities();

  if (!userOrClient) {
    return (
      <Typography component="span" variant="inherit" sx={{ color: "text.secondary" }}>
        {t("anonymous")}
      </Typography>
    );
  }

  const isMachineClient = userOrClient.machineClientId != null;
  const Icon = isMachineClient ? SmartToyOutlinedIcon : PersonOutlinedIcon;
  const kindLabel = t(isMachineClient ? "machineClient" : "user");
  return (
    <Stack
      component="span"
      direction="row"
      sx={{ alignItems: "center", gap: 1, minWidth: 0 }}
      data-cy={isMachineClient ? "userOrClient-machineClient" : "userOrClient-person"}>
      {machineDeliveryEnabled && (
        <Icon fontSize="small" titleAccess={kindLabel} color="primary" sx={{ flexShrink: 0 }} />
      )}
      {path ? (
        <Link component={RouterLink} to={path} noWrap>
          {userOrClient.name}
        </Link>
      ) : (
        <Typography component="span" variant="inherit" noWrap>
          {userOrClient.name}
        </Typography>
      )}
    </Stack>
  );
};
