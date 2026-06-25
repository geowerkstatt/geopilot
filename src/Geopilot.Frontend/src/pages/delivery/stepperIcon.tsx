import { FC } from "react";
import CheckIcon from "@mui/icons-material/Check";
import WarningIcon from "@mui/icons-material/Warning";
import { CircularProgress } from "@mui/material";
import { geopilotTheme } from "../../appTheme.ts";
import { FlexRowBox } from "../../components/styledComponents.ts";

interface StepperIconProps {
  index: number;
  open?: boolean;
  enabled?: boolean;
  completed?: boolean;
  error?: boolean;
  isLoading?: boolean;
}

export const StepperIcon: FC<StepperIconProps> = ({ index, open, enabled, completed, error, isLoading }) => {
  return (
    <FlexRowBox sx={{ position: "relative", justifyContent: "center" }} {...(open ? { "data-cy": "active" } : {})}>
      {error ? (
        <WarningIcon color="error" sx={{ fontSize: { xs: 24, md: 28 } }} data-cy="stepper-error" />
      ) : (
        <>
          <FlexRowBox
            sx={{
              justifyContent: "center",
              borderRadius: "50%",
              width: "24px",
              height: "24px",
              lineHeight: "24px",
              backgroundColor:
                enabled || completed ? geopilotTheme.palette.primary.main : geopilotTheme.palette.primary.inactive,
              color: geopilotTheme.palette.primary.contrastText,
              alignItems: "center",
              fontSize: "12px",
            }}
            data-cy={`stepper-${completed ? "completed" : "number"}`}>
            {completed ? <CheckIcon fontSize="small" /> : index + 1}
          </FlexRowBox>
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
    </FlexRowBox>
  );
};
