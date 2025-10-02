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
import { ValidationStatus } from "../../../api/apiInterfaces";

export const DeliveryValidationNotLoggedIn = () => {
  const { resetDelivery, validateFile, isLoading, validationResponse } = useContext(DeliveryContext);
  const { login, authEnabled } = useGeopilotAuth();

  const isStartingJob = isLoading && validationResponse?.status === ValidationStatus.Ready;
  const isValidating = isLoading && validationResponse?.status === ValidationStatus.Processing;

  if (isValidating) {
    return <DeliveryValidationLoading />;
  }

  if (isValidationFinished(validationResponse)) {
    return <DeliveryValidationResults />;
  }

  return (
    <FlexRowEndBox>
      <CancelButton onClick={() => resetDelivery()} />
      {validationResponse?.jobId && (
        <BaseButton
          onClick={() => validateFile(validationResponse.jobId, {})}
          icon={<PublishedWithChangesIcon />}
          label="validateOnly"
          disabled={isStartingJob}
        />
      )}
      {authEnabled && (
        <BaseButton onClick={login} icon={<LoginIcon />} label="logInForDelivery" disabled={isStartingJob} />
      )}
    </FlexRowEndBox>
  );
};
