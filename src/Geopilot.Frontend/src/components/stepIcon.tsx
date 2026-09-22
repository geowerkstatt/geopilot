import { FC } from "react";
import { useTranslation } from "react-i18next";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorIcon from "@mui/icons-material/Error";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RemoveCircleIcon from "@mui/icons-material/RemoveCircle";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import WarningIcon from "@mui/icons-material/Warning";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { CircularProgress, Stack, useTheme } from "@mui/material";
import { DeliveryStepState } from "../api/apiInterfaces.ts";
import { StepState } from "../api/generated";
import { NEOGEO_FONTWEIGHT_BOLD, NEOGEO_FONTWEIGHT_REGULAR, px2rem } from "../appTheme.ts";

type StepIconVariant = "contained" | "outlined";

interface StepIconProps {
  step: number;
  state: StepState | DeliveryStepState;
  variant: StepIconVariant;
}

const stateLabelKey: Record<StepState | DeliveryStepState, string> = {
  [DeliveryStepState.Enabled]: "stepStateEnabled",
  [StepState.Pending]: "stepStatePending",
  [StepState.Running]: "stepStateRunning",
  [StepState.Skipped]: "stepStateSkipped",
  [StepState.Success]: "stepStateFinished",
  [StepState.Error]: "stepStateFailed",
  [StepState.Cancelled]: "stepStateCancelled",
  [StepState.Warning]: "stepStateWarning",
  [StepState.DeliveryRestriction]: "stepStateDeliveryRestriction",
};

export const StepIcon: FC<StepIconProps> = ({ step, state, variant }) => {
  const theme = useTheme();
  const { t } = useTranslation();
  const isOutlined = variant === "outlined";

  const renderContent = () => {
    switch (state) {
      case StepState.Success:
        return isOutlined ? <CheckCircleOutlineIcon color="primary" /> : <CheckCircleIcon color="primary" />;
      case StepState.Warning:
        return isOutlined ? <WarningAmberIcon color="warning" /> : <WarningIcon color="warning" />;
      case StepState.Error:
      case StepState.Cancelled:
        return isOutlined ? <ErrorOutlineIcon color="error" /> : <ErrorIcon color="error" />;
      case StepState.DeliveryRestriction:
        return <BlockIcon color="error" />;
      case StepState.Skipped:
        return isOutlined ? (
          <RemoveCircleOutlineIcon sx={{ color: theme.palette.primary.states.disabledBackground }} />
        ) : (
          <RemoveCircleIcon sx={{ color: theme.palette.primary.states.disabledBackground }} />
        );
      case DeliveryStepState.Enabled:
      case StepState.Pending:
      case StepState.Running: {
        const baseColor =
          state === StepState.Pending ? theme.palette.primary.states.disabledBackground : theme.palette.primary.main;
        return (
          <>
            <Stack
              sx={{
                width: px2rem(20),
                height: px2rem(20),
                alignItems: "center",
                justifyContent: "center",
                fontSize: px2rem(14),
                fontWeight: isOutlined ? NEOGEO_FONTWEIGHT_BOLD : NEOGEO_FONTWEIGHT_REGULAR,
                lineHeight: 1,
                borderRadius: theme.radius.full,
                backgroundColor: isOutlined ? theme.palette.primary.contrast : baseColor,
                color: isOutlined ? baseColor : theme.palette.primary.contrast,
                border: isOutlined ? `2px solid ${baseColor}` : undefined,
              }}>
              {step}
            </Stack>
            {state === StepState.Running && (
              <CircularProgress
                size={px2rem(28)}
                color="primary"
                aria-hidden
                data-cy="stepIcon-loading"
                sx={{ position: "absolute" }}
              />
            )}
          </>
        );
      }
    }
  };

  return (
    <Stack
      role="img"
      aria-label={t(stateLabelKey[state])}
      data-cy={`stepIcon-${state}`}
      sx={{
        width: px2rem(28),
        height: px2rem(28),
        position: "relative",
        direction: "row",
        alignItems: "center",
        justifyContent: "center",
      }}>
      {renderContent()}
    </Stack>
  );
};
