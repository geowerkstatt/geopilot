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
  padding: "20px 40px",
}));

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep, isLoading, error } = useContext(DeliveryContext);

  const isOpen = (step: number, keepOpen?: boolean) => activeStep === step || (activeStep >= step && keepOpen);
  const isCompleted = (step: number) => activeStep > step;

  return (
    <CenteredBox>
      <Typography variant="h1">{t("deliveryTitle")}</Typography>
      <DeliveryContainer>
        <Stepper activeStep={activeStep} orientation="vertical">
          {steps.map((step, index) => (
            <Step key={step.label} active={isOpen(index, step.keepOpen)} completed={isCompleted(index)}>
              <StepLabel
                error={index === activeStep && !!error}
                StepIconComponent={props => (
                  <StepperIcon index={index} stepIconProps={props} isLoading={index === activeStep && isLoading} />
                )}>
                {t(step.label)}
                {index === activeStep && !!error && ` - ${error}`}
              </StepLabel>
              <StepContent>{step.content}</StepContent>
            </Step>
          ))}
        </Stepper>
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
