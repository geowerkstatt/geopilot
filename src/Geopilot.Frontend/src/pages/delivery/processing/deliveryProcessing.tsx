import { useContext, useEffect } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";
import { DeliveryContent } from "../deliveryContent";
import { CancelButton } from "../../../components/buttons";
import { isProcessingDeliverable } from "../deliveryUtils";
import { DeliveryBackButton, DeliveryContinueButton } from "../deliveryButtons";
import { DeliveryStepEnum } from "../deliveryInterfaces";

export const DeliveryProcessing = () => {
  const { activeStep, isLoading, isProcessing, processingResponse, steps, resetDelivery, continueToNextStep } =
    useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

  const lastStep = Array.from(steps.keys())[steps.size - 1];
  const isLastStep = lastStep === DeliveryStepEnum.Processing;
  const lastStepIsActive = activeStep === steps.size - 1;
  useEffect(() => {
    if (isLastStep && lastStepIsActive && !isLoading && !isProcessing) {
      // Mark this step as completed if there is no next step
      continueToNextStep();
    }
  }, [continueToNextStep, isLastStep, lastStepIsActive, isLoading, isProcessing]);

  const buttons = isProcessing ? (
    <CancelButton onClick={resetDelivery} />
  ) : (
    <>
      <DeliveryBackButton />
      {isProcessingDeliverable(processingResponse) && <DeliveryContinueButton />}
    </>
  );

  return (
    <DeliveryContent title="processing" buttons={buttons}>
      {isProcessing && <DeliveryProcessingLoading />}
      {hasSteps && <DeliveryProcessingResults />}
    </DeliveryContent>
  );
};
