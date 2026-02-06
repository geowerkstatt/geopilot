import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryValidationForm } from "./deliveryValidationForm";
import { DeliveryValidationLoading } from "./deliveryValidationLoading";
import { DeliveryValidationResults } from "./deliveryValidationResults";
import { isValidationFinished } from "../deliveryUtils";
import { FlexBox } from "../../../components/styledComponents";
import { ValidationStatus } from "../../../api/apiInterfaces";

export const DeliveryValidation = () => {
  const { isLoading, validationResponse } = useContext(DeliveryContext);

  const isValidating = isLoading && validationResponse?.status === ValidationStatus.Processing;

  return (
    <FlexBox>
      <DeliveryValidationForm />
      {isValidating && <DeliveryValidationLoading />}
      {isValidationFinished(validationResponse) && <DeliveryValidationResults />}
    </FlexBox>
  );
};
