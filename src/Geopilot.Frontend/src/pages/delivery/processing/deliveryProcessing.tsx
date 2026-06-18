import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";
import { DeliveryContent } from "../deliveryContent";
import { CancelButton } from "../../../components/buttons";
import { isProcessingDeliverable } from "../deliveryUtils";
import { DeliveryBackButton, DeliveryContinueButton } from "../deliveryButtons";

export const DeliveryProcessing = () => {
  const { isProcessing, processingResponse, resetDelivery } = useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

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
