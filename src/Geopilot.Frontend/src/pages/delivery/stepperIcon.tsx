import { FC } from "react";
import CheckIcon from "@mui/icons-material/Check";
import WarningIcon from "@mui/icons-material/Warning";
import { CircularProgress, Stack, useTheme } from "@mui/material";

interface StepperIconProps {
  index: number;
  open?: boolean;
  enabled?: boolean;
  completed?: boolean;
  error?: boolean;
  isLoading?: boolean;
}

export const StepperIcon: FC<StepperIconProps> = ({ index, open, enabled, completed, error, isLoading }) => {
  const theme = useTheme();

  return (
    <Stack
      direction="row"
      sx={{ alignItems: "center", flexWrap: "wrap", position: "relative", justifyContent: "center" }}
      {...(open ? { "data-cy": "active" } : {})}>
      {error ? (
        <WarningIcon color="error" sx={{ fontSize: { xs: 24, md: 28 } }} data-cy="stepper-error" />
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
