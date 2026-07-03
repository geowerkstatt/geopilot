import { FC, useContext } from "react";
import { ButtonProps } from "@mui/material/Button";
import { Button } from "../../components/buttons";
import { DeliveryContext } from "./deliveryContext";

export const DeliveryBackButton = () => {
  const { activeStep, showCompletedOrNextStep } = useContext(DeliveryContext);

  return activeStep > 0 && <Button onClick={() => showCompletedOrNextStep(activeStep - 1)} label="back" />;
};

export const DeliveryContinueButton: FC<Omit<ButtonProps, "onClick" | "label">> = props => {
  const { activeStep, steps, continueToNextStep } = useContext(DeliveryContext);

  return (
    activeStep < steps.size - 1 && (
      <Button {...props} onClick={continueToNextStep} label="continue" variant="contained" />
    )
  );
};
