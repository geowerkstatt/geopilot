import { FC } from "react";
import BlockIcon from "@mui/icons-material/Block";
import CheckIcon from "@mui/icons-material/Check";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutline";
import WarningIcon from "@mui/icons-material/Warning";
import { CircularProgress, Stack, useTheme } from "@mui/material";
import { DeliveryStepStatus } from "./deliveryInterfaces";

interface StepperIconProps {
  index: number;
  open?: boolean;
  enabled?: boolean;
  completed?: boolean;
  status?: DeliveryStepStatus;
  isLoading?: boolean;
}

export const StepperIcon: FC<StepperIconProps> = ({ index, open, enabled, completed, status, isLoading }) => {
  const theme = useTheme();

  return (
    <Stack
      direction="row"
      sx={{ alignItems: "center", flexWrap: "wrap", position: "relative", justifyContent: "center" }}
      {...(open ? { "data-cy": "active" } : {})}>
      {status === "error" ? (
        <WarningIcon color="error" sx={{ fontSize: { xs: 24, md: 28 } }} data-cy="stepper-error" />
      ) : status === "deliveryRestriction" ? (
        <BlockIcon color="error" sx={{ fontSize: { xs: 24, md: 28 } }} data-cy="stepper-deliveryRestriction" />
      ) : status === "warning" ? (
        <WarningIcon color="warning" sx={{ fontSize: { xs: 24, md: 28 } }} data-cy="stepper-warning" />
      ) : status === "skipped" ? (
        <RemoveCircleOutlineIcon
          sx={{ fontSize: { xs: 24, md: 28 }, color: theme.palette.primary.states.disabledBackground }}
          data-cy="stepper-skipped"
        />
      ) : (
        <>
          <Stack
            direction="row"
            sx={{
              flexWrap: "wrap",
              justifyContent: "center",
              borderRadius: theme.radius.full,
              width: "24px",
              height: "24px",
              lineHeight: "24px",
              backgroundColor:
                enabled || completed ? theme.palette.primary.main : theme.palette.primary.states.disabledBackground,
              color: theme.palette.primary.contrast,
              alignItems: "center",
              fontSize: "12px",
            }}
            data-cy={`stepper-${completed ? "completed" : "number"}`}>
            {completed ? <CheckIcon fontSize="small" /> : index + 1}
          </Stack>
          {enabled && !completed && isLoading && (
            <CircularProgress
              size={32}
              color="primary"
              sx={{
                position: "absolute",
              }}
              data-cy="stepper-loading"
            />
          )}
        </>
      )}
    </Stack>
  );
};
