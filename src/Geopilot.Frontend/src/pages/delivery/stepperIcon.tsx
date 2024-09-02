import { CircularProgress, StepIconProps } from "@mui/material";
import { FC } from "react";
import { FlexRowCenterBox } from "../../components/styledComponents.ts";
import WarningIcon from "@mui/icons-material/Warning";
import { geopilotTheme } from "../../appTheme.ts";
import CheckIcon from "@mui/icons-material/Check";

interface StepperIconProps {
  index: number;
  stepIconProps: StepIconProps;
  isLoading?: boolean;
}

export const StepperIcon: FC<StepperIconProps> = ({ index, stepIconProps, isLoading }) => {
  return (
    <FlexRowCenterBox sx={{ position: "relative" }}>
      {stepIconProps.error ? (
        <WarningIcon color="error" sx={{ fontSize: 28 }} data-cy="stepper-error" />
      ) : (
        <>
          <FlexRowCenterBox
            sx={{
              borderRadius: "50%",
              width: "24px",
              height: "24px",
              backgroundColor:
                stepIconProps.active || stepIconProps.completed
                  ? geopilotTheme.palette.primary.main
                  : geopilotTheme.palette.primary.inactive,
              color: geopilotTheme.palette.primary.contrastText,
              alignItems: "center",
              fontSize: "12px",
            }}
            data-cy={`stepper-${stepIconProps.completed ? "completed" : "number"}`}>
            {stepIconProps.completed ? <CheckIcon fontSize="small" /> : index + 1}
          </FlexRowCenterBox>
          {stepIconProps.active && isLoading && (
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
    </FlexRowCenterBox>
  );
};
