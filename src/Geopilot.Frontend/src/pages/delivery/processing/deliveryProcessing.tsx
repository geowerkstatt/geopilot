import { FC, useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";
import { DeliveryContent } from "../deliveryContent";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { isProcessingDeliverable } from "../deliveryUtils";
import { DeliveryStepProps } from "../deliveryInterfaces";

export const DeliveryProcessing: FC<DeliveryStepProps> = ({ completed }) => {
  const { isProcessing, processingResponse, continueToNextStep, resetDelivery } = useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

  const buttons = (
    <>
      <CancelButton onClick={resetDelivery} />
      {!isProcessing && isProcessingDeliverable(processingResponse) && (
        <BaseButton disabled={completed} onClick={() => continueToNextStep()} label="continue" />
      )}
    </>
  );

  return (
    <DeliveryContent title="process" buttons={buttons}>
      {isProcessing && <DeliveryProcessingLoading />}
      {hasSteps && <DeliveryProcessingResults />}
    </DeliveryContent>
  );
};
