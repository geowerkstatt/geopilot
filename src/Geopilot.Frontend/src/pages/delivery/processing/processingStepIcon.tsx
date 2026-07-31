import { FC, ReactElement } from "react";
import { useTranslation } from "react-i18next";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutline";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Box, CircularProgress, Stack, Tooltip, Typography } from "@mui/material";
import { StepState } from "../../../api/apiInterfaces";
import { geopilotTheme } from "../../../appTheme";

interface ProcessingStepIconProps {
  state: StepState;
  index: number;
  message?: string;
}

const ICON_SIZE = 28;

const stateTranslationKey: Record<StepState, string> = {
  [StepState.Pending]: "stepStatePending",
  [StepState.Running]: "stepStateRunning",
  [StepState.Skipped]: "stepStateSkipped",
  [StepState.Success]: "stepStateFinished",
  [StepState.Error]: "stepStateFailed",
  [StepState.Cancelled]: "stepStateCancelled",
  [StepState.Warning]: "stepStateWarning",
  [StepState.DeliveryRestriction]: "stepStateDeliveryRestriction",
};

const renderIcon = (state: StepState, index: number): ReactElement => {
  switch (state) {
    case StepState.Success:
      return (
        <CheckCircleOutlineIcon
          sx={{ fontSize: ICON_SIZE, color: geopilotTheme.palette.primary.main }}
          data-cy="processing-step-icon-success"
        />
      );
    case StepState.Warning:
      return (
        <WarningAmberIcon
          sx={{ fontSize: ICON_SIZE, color: geopilotTheme.palette.warning.main }}
          data-cy="processing-step-icon-warning"
        />
      );
    // Delivery-restriction state: a distinct block icon in the error colour, since delivery is blocked.
    case StepState.DeliveryRestriction:
      return (
        <BlockIcon
          sx={{ fontSize: ICON_SIZE, color: geopilotTheme.palette.error.main }}
          data-cy="processing-step-icon-deliveryrestriction"
        />
      );
    case StepState.Error:
    case StepState.Cancelled:
      return (
        <ErrorOutlineIcon
          sx={{ fontSize: ICON_SIZE, color: geopilotTheme.palette.error.main }}
          data-cy="processing-step-icon-error"
        />
      );
    case StepState.Skipped:
      return (
        <RemoveCircleOutlineIcon
          sx={{ fontSize: ICON_SIZE, color: geopilotTheme.palette.primary.states.disabledBackground }}
          data-cy="processing-step-icon-skipped"
        />
      );
    case StepState.Running:
      return (
        <Box sx={{ position: "relative", width: ICON_SIZE, height: ICON_SIZE }} data-cy="processing-step-icon-running">
          <CircularProgress size={ICON_SIZE} sx={{ position: "absolute", color: geopilotTheme.palette.primary.main }} />
          <Stack
            direction="row"
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
              justifyContent: "center",
              width: "100%",
              height: "100%",
            }}>
            <Typography
              variant="caption"
              sx={{ color: geopilotTheme.palette.primary.main, fontWeight: 600, lineHeight: 1 }}>
              {index + 1}
            </Typography>
          </Stack>
        </Box>
      );
    case StepState.Pending:
    default:
      return (
        <Stack
          direction="row"
          sx={{
            alignItems: "center",
            flexWrap: "wrap",
            justifyContent: "center",
            width: ICON_SIZE,
            height: ICON_SIZE,
            borderRadius: geopilotTheme.radius.full,
            border: `2px solid ${geopilotTheme.palette.primary.states.disabledBackground}`,
          }}
          data-cy="processing-step-icon-pending">
          <Typography
            variant="caption"
            sx={{ color: geopilotTheme.palette.primary.states.disabledBackground, fontWeight: 600, lineHeight: 1 }}>
            {index + 1}
          </Typography>
        </Stack>
      );
  }
};

export const ProcessingStepIcon: FC<ProcessingStepIconProps> = ({ state, index, message }) => {
  const { t } = useTranslation();
  return (
    <Tooltip title={message ?? t(stateTranslationKey[state])} arrow>
      <Box sx={{ display: "inline-flex" }}>{renderIcon(state, index)}</Box>
    </Tooltip>
  );
};
