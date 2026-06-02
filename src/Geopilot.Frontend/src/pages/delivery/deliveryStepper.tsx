import { Stack, Typography } from "@mui/material";
import { styled } from "@mui/system";
import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { GeopilotBox } from "../../components/styledComponents";
import { DeliveryContext } from "./deliveryContext";
import { StepperIcon } from "./stepperIcon";

const StepperStack = styled(Stack)({
  minWidth: 300,
  flex: 0,
});

const DeliveryStepBox = styled(GeopilotBox, { shouldForwardProp: prop => prop !== "open" })<{ open: boolean }>(
  ({ open, theme }) => ({
    backgroundColor: open ? theme.palette.primary.selected : "white",
  }),
);

export const DeliveryStepper = () => {
  const { t } = useTranslation();
  const { steps, activeStep, isLoading, isProcessing } = useContext(DeliveryContext);

  const isOpen = (stepIndex: number) => activeStep === stepIndex;
  const isCompleted = (stepIndex: number) => activeStep > stepIndex;

  return (
    <StepperStack spacing={2}>
      {Array.from(steps.entries()).map(([key, step], index) => (
        <DeliveryStepBox key={key} data-cy={`${key}-step`} direction="row" alignItems="flex-start" open={isOpen(index)}>
          <StepperIcon
            index={index}
            active={isOpen(index)}
            completed={isCompleted(index)}
            error={!!step.error}
            isLoading={isOpen(index) && (isLoading || isProcessing)}
          />
          <Stack spacing={1}>
            <Typography
              variant="h3"
              m={0}
              color={isOpen(index) || isCompleted(index) ? "textPrimary" : "textSecondary"}>
              {t(step.label)}
            </Typography>
            {step.labelAddition && (
              <Typography variant="body2" color="textSecondary">
                {t(step.labelAddition)}
              </Typography>
            )}
            {step.error && (
              <Typography variant="body2" color="error">
                {t(step.error)}
              </Typography>
            )}
          </Stack>
        </DeliveryStepBox>
      ))}
    </StepperStack>
  );
};
