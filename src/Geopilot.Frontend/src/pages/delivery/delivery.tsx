import { Box, Step, StepContent, StepLabel, Stepper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox } from "../../components/styledComponents.ts";
import { StepperIcon } from "./stepperIcon.tsx";
import { styled } from "@mui/system";
import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";

const DeliveryContainer = styled(Box)(({ theme }) => ({
  backgroundColor: theme.palette.primary.hover,
  border: `1px solid ${theme.palette.primary.main}`,
  borderRadius: "4px",
  padding: "40px",
}));

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep, isLoading } = useContext(DeliveryContext);

  const isOpen = (stepIndex: number, keepOpen?: boolean) =>
    activeStep === stepIndex || (activeStep >= stepIndex && keepOpen);
  const isCompleted = (stepIndex: number) => activeStep > stepIndex;

  return (
    <CenteredBox>
      <Typography variant="h1">{t("deliveryTitle")}</Typography>
      <DeliveryContainer>
        <Stepper activeStep={activeStep} orientation="vertical">
          {Array.from(steps.entries()).map(([key, step], index) => {
            if (step) {
              return (
                <Step key={key} active={isOpen(index, step.keepOpen)} completed={isCompleted(index)}>
                  <StepLabel
                    error={!!step.error}
                    StepIconComponent={props => (
                      <StepperIcon index={index} stepIconProps={props} isLoading={index === activeStep && isLoading} />
                    )}>
                    {t(step.label)}
                    {step.labelAddition && step.labelAddition.length > 0 && (
                      <i>{` - ${t(step.labelAddition || "")}`}</i>
                    )}{" "}
                    {step.error && ` - ${step.error}`}
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
