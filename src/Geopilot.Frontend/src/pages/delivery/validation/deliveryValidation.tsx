import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext";
import { DeliveryValidationForm } from "./deliveryValidationForm";
import { DeliveryValidationLoading } from "./deliveryValidationLoading";
import { DeliveryValidationResults } from "./deliveryValidationResults";
import { isValidationFinished } from "../deliveryUtils";
import { FlexBox } from "../../../components/styledComponents";

export const DeliveryValidation = () => {
  const { isValidating, validationResponse } = useContext(DeliveryContext);

  return (
    <FlexBox>
      <DeliveryValidationForm />
      {isValidating && <DeliveryValidationLoading />}
      {isValidationFinished(validationResponse) && <DeliveryValidationResults />}
    </FlexBox>
  );
};
