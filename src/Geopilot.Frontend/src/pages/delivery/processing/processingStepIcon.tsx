import { FC, ReactElement } from "react";
import { useTranslation } from "react-i18next";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutline";
import { Box, CircularProgress, Stack, Tooltip, Typography } from "@mui/material";
import { StepState } from "../../../api/apiInterfaces";
import { geopilotTheme } from "../../../appTheme";

interface ProcessingStepIconProps {
  state: StepState;
  index: number;
}

const ICON_SIZE = 28;

const stateTranslationKey: Record<StepState, string> = {
  [StepState.Pending]: "stepStatePending",
  [StepState.Running]: "stepStateRunning",
  [StepState.Skipped]: "stepStateSkipped",
  [StepState.Success]: "stepStateFinished",
  [StepState.Error]: "stepStateFailed",
  [StepState.Cancelled]: "stepStateCancelled",
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
            borderRadius: "50%",
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

export const ProcessingStepIcon: FC<ProcessingStepIconProps> = ({ state, index }) => {
  const { t } = useTranslation();
  return (
    <Tooltip title={t(stateTranslationKey[state])} arrow>
      <Box sx={{ display: "inline-flex" }}>{renderIcon(state, index)}</Box>
    </Tooltip>
  );
};
