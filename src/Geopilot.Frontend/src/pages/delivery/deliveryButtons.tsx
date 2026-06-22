import { useContext } from "react";
import { DeliveryContext } from "./deliveryContext";
import { BaseButton } from "../../components/buttons";

export const DeliveryBackButton = () => {
  const { activeStep, showCompletedOrNextStep } = useContext(DeliveryContext);

  return (
    activeStep > 0 && (
      <BaseButton onClick={() => showCompletedOrNextStep(activeStep - 1)} label="back" variant="outlined" />
    )
  );
};

export const DeliveryContinueButton = () => {
  const { activeStep, steps, continueToNextStep } = useContext(DeliveryContext);

  return activeStep < steps.size - 1 && <BaseButton onClick={continueToNextStep} label="continue" />;
};
