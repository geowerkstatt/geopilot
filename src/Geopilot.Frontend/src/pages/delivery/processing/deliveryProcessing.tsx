import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryProcessingForm } from "./deliveryProcessingForm";
import { DeliveryProcessingLoading } from "./deliveryProcessingLoading";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";
import { FlexBox } from "../../../components/styledComponents";

export const DeliveryProcessing = () => {
  const { isProcessing, processingResponse } = useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

  return (
    <FlexBox>
      <DeliveryProcessingForm />
      {isProcessing && <DeliveryProcessingLoading />}
      {hasSteps && <DeliveryProcessingResults />}
    </FlexBox>
  );
};
