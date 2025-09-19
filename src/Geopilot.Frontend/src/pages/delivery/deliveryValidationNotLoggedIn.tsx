import { useContext } from "react";
import { BaseButton, CancelButton } from "../../components/buttons";
import { FlexRowEndBox } from "../../components/styledComponents";
import LoginIcon from "@mui/icons-material/Login";
import PublishedWithChangesIcon from "@mui/icons-material/PublishedWithChanges";
import { DeliveryContext } from "./deliveryContext";
import { useGeopilotAuth } from "../../auth";
import { ValidationStatus } from "../../api/apiInterfaces";
import { DeliveryValidationLoading } from "./deliveryValidationLoading";
import { DeliveryValidationResults } from "./deliveryValidationResults";

export const DeliveryValidationNotLoggedIn = () => {
  const { resetDelivery, validateFile, isLoading, validationResponse } = useContext(DeliveryContext);
  const { login } = useGeopilotAuth();

  const isValidationFinished = () => {
    const status = validationResponse?.status;
    return (
      status === ValidationStatus.Completed ||
      status === ValidationStatus.CompletedWithErrors ||
      status === ValidationStatus.Failed
    );
  };

  if (isLoading) {
    return <DeliveryValidationLoading />;
  }

  if (isValidationFinished()) {
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
