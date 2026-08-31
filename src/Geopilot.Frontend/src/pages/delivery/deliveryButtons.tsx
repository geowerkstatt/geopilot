import { FC, useContext } from "react";
import { ButtonProps } from "@mui/material/Button";
import { Button } from "../../components/buttons";
import { DeliveryContext } from "./deliveryContext";

/**
 * Advances to the next step. It is only offered on the frontier of the process: a step the user has
 * already moved past is revisited through the stepper and offers no way forward of its own.
 */
export const DeliveryContinueButton: FC<Omit<ButtonProps, "onClick" | "label">> = props => {
  const { activeStep, furthestVisitedStep, steps, continueToNextStep } = useContext(DeliveryContext);
  const isOnFrontier = activeStep === furthestVisitedStep;
  const hasNextStep = activeStep < steps.size - 1;

  return (
    isOnFrontier &&
    hasNextStep && <Button {...props} onClick={continueToNextStep} label="continue" variant="contained" />
  );
};
