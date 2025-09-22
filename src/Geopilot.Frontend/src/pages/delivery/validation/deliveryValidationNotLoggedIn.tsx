import { useContext } from "react";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { FlexRowEndBox } from "../../../components/styledComponents";
import LoginIcon from "@mui/icons-material/Login";
import PublishedWithChangesIcon from "@mui/icons-material/PublishedWithChanges";
import { DeliveryContext } from "../deliveryContext";
import { useGeopilotAuth } from "../../../auth";
import { DeliveryValidationLoading } from "./deliveryValidationLoading";
import { DeliveryValidationResults } from "./deliveryValidationResults";
import { isValidationFinished } from "../deliveryUtils";

export const DeliveryValidationNotLoggedIn = () => {
  const { resetDelivery, validateFile, isLoading, validationResponse } = useContext(DeliveryContext);
  const { login } = useGeopilotAuth();

  if (isLoading) {
    return <DeliveryValidationLoading />;
  }

  if (isValidationFinished(validationResponse)) {
    return <DeliveryValidationResults />;
  }

  return (
    <FlexRowEndBox>
      <CancelButton onClick={() => resetDelivery()} />
      <BaseButton
        onClick={() => validateFile(validationResponse!.jobId, {})}
        icon={<PublishedWithChangesIcon />}
        label="validateOnly"
      />
      <BaseButton onClick={login} icon={<LoginIcon />} label="logInForDelivery" />
    </FlexRowEndBox>
  );
};
