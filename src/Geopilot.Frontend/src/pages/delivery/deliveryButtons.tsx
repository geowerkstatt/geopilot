import { FC, useContext } from "react";
import { ButtonProps } from "@mui/material/Button";
import { BaseButton } from "../../components/buttons";
import { DeliveryContext } from "./deliveryContext";

export const DeliveryBackButton = () => {
  const { activeStep, showCompletedOrNextStep } = useContext(DeliveryContext);

  return (
    activeStep > 0 && (
      <BaseButton onClick={() => showCompletedOrNextStep(activeStep - 1)} label="back" variant="outlined" />
    )
  );
};

export const DeliveryContinueButton: FC<Omit<ButtonProps, "onClick" | "label">> = props => {
  const { activeStep, steps, continueToNextStep } = useContext(DeliveryContext);

  return activeStep < steps.size - 1 && <BaseButton {...props} onClick={continueToNextStep} label="continue" />;
};
