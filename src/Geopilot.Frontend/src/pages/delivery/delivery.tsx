import { Step, StepContent, StepLabel, Stepper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox, GeopilotBox } from "../../components/styledComponents.ts";
import { StepperIcon } from "./stepperIcon.tsx";
import { styled } from "@mui/system";
import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";

export const DeliveryContainer = styled(GeopilotBox)(({ theme }) => ({
  backgroundColor: theme.palette.primary.hover,
  padding: "40px",
  flex: 1,
}));

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep, isLoading, isValidating } = useContext(DeliveryContext);

  const isOpen = (stepIndex: number, keepOpen?: boolean) =>
    activeStep === stepIndex || (activeStep >= stepIndex && keepOpen);
  const isCompleted = (stepIndex: number) => activeStep > stepIndex;

  return (
    <CenteredBox data-cy="delivery">
      <Typography variant="h1">{t("deliveryTitle")}</Typography>
      <DeliveryContainer>
        <Stepper activeStep={activeStep} orientation="vertical">
          {Array.from(steps.entries()).map(([key, step], index) => {
            if (step) {
              return (
                <Step
                  key={key}
                  active={isOpen(index, step.keepOpen)}
                  completed={isCompleted(index)}
                  data-cy={`${key}-step`}>
                  <StepLabel
                    error={!!step.error}
                    StepIconComponent={props => (
                      <StepperIcon index={index} stepIconProps={props} isLoading={index === activeStep && (isLoading || isValidating)} />
                    )}>
                    {t(step.label)}
                    {step.labelAddition && step.labelAddition.length > 0 && (
                      <i>{` - ${t(step.labelAddition || "")}`}</i>
                    )}{" "}
                    {step.error && ` - ${t(step.error)}`}
                  </StepLabel>
                  <StepContent>{step.content}</StepContent>
                </Step>
              );
            }
          })}
        </Stepper>
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
