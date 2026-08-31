import { FC, useContext } from "react";
import { ButtonProps } from "@mui/material/Button";
import { Button } from "../../components/buttons";
import { DeliveryContext } from "./deliveryContext";

export const DeliveryContinueButton: FC<Omit<ButtonProps, "onClick" | "label">> = props => {
  const { activeStep, furthestVisitedStep, steps, continueToNextStep } = useContext(DeliveryContext);
  const isOnFrontier = activeStep === furthestVisitedStep;
  const hasNextStep = activeStep < steps.size - 1;

  return (
    isOnFrontier &&
    hasNextStep && <Button {...props} onClick={continueToNextStep} label="continue" variant="contained" />
  );
};
