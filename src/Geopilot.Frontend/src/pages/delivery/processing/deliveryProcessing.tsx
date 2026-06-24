import { useContext } from "react";
import { DeliveryBackButton, DeliveryContinueButton } from "../deliveryButtons";
import { DeliveryContent } from "../deliveryContent";
import { DeliveryContext } from "../deliveryContext";
import { isProcessingDeliverable } from "../deliveryUtils";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";

export const DeliveryProcessing = () => {
  const { isProcessing, processingResponse } = useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

  const buttons = (
    <>
      <DeliveryBackButton />
      <DeliveryContinueButton disabled={isProcessing || !isProcessingDeliverable(processingResponse)} />
    </>
  );

  return (
    <DeliveryContent title="processing" buttons={buttons}>
      {isProcessing && <DeliveryProcessingLoading />}
      {hasSteps && <DeliveryProcessingResults />}
    </DeliveryContent>
  );
};
