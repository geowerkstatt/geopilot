import { useContext } from "react";
import { useMediaQuery, useTheme } from "@mui/material";
import { DeliveryBackButton, DeliveryContinueButton } from "../deliveryButtons";
import { DeliveryContent } from "../deliveryContent";
import { DeliveryContext } from "../deliveryContext";
import { isProcessingDeliverable } from "../deliveryUtils";
import { DeliveryProcessingResults } from "./deliveryProcessingResults";

export const DeliveryProcessing = () => {
  const { isProcessing, processingResponse } = useContext(DeliveryContext);
  const hasSteps = (processingResponse?.steps?.length ?? 0) > 0;

  const theme = useTheme();
  const isXs = useMediaQuery(theme.breakpoints.down("sm"));

  const buttons = (
    <>
      <DeliveryBackButton />
      <DeliveryContinueButton disabled={isProcessing || !isProcessingDeliverable(processingResponse)} />
    </>
  );

  return (
    <DeliveryContent title="processing" buttons={buttons} hideBox={isXs}>
      {hasSteps && <DeliveryProcessingResults />}
    </DeliveryContent>
  );
};
