import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import PersonOutlinedIcon from "@mui/icons-material/PersonOutlined";
import SmartToyOutlinedIcon from "@mui/icons-material/SmartToyOutlined";
import { Link, Stack, Typography } from "@mui/material";
import { useCapabilities } from "./capabilities/capabilitiesInterface.ts";

interface DeclarerLabelProps {
  /** A machine client when `machineClientId` is set, a user otherwise. */
  declarer?: { name: string; machineClientId?: number | null } | null;
  /** Where the name links to; without it the name is plain text. */
  path?: string;
}

/**
 * Who declared a delivery or started a run, or "anonymous". Where machine delivery is offered, an icon tells a user
 * from a machine client.
 */
export const DeclarerLabel: FC<DeclarerLabelProps> = ({ declarer, path }) => {
  const { t } = useTranslation();
  const { machineDeliveryEnabled } = useCapabilities();

  if (!declarer) {
    return (
      <Typography component="span" variant="inherit" sx={{ color: "text.secondary" }}>
        {t("anonymous")}
      </Typography>
    );
  }

  const isMachineClient = declarer.machineClientId != null;
  const Icon = isMachineClient ? SmartToyOutlinedIcon : PersonOutlinedIcon;
  const kindLabel = t(isMachineClient ? "declarerKindMachineClient" : "declarerKindPerson");
  return (
    <Stack
      component="span"
      direction="row"
      sx={{ alignItems: "center", gap: 1, minWidth: 0 }}
      data-cy={isMachineClient ? "declarer-machineClient" : "declarer-person"}>
      {machineDeliveryEnabled && (
        <Icon fontSize="small" titleAccess={kindLabel} color="primary" sx={{ flexShrink: 0 }} />
      )}
      {path ? (
        <Link component={RouterLink} to={path} noWrap>
          {declarer.name}
        </Link>
      ) : (
        <Typography component="span" variant="inherit" noWrap>
          {declarer.name}
        </Typography>
      )}
    </Stack>
  );
};
