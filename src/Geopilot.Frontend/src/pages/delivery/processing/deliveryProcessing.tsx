import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryProcessingForm } from "./deliveryProcessingForm";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";
import { isProcessingFinished } from "../deliveryUtils";
import { FlexBox } from "../../../components/styledComponents";

export const DeliveryProcessing = () => {
  const { isProcessing, processingResponse } = useContext(DeliveryContext);

  return (
    <FlexBox>
      <DeliveryProcessingForm />
      {isProcessing && <DeliveryProcessingLoading />}
      {isProcessingFinished(processingResponse) && <DeliveryProcessingResults />}
    </FlexBox>
  );
};
