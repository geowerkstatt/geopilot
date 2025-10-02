import { useContext } from "react";
import { DeliveryContext } from "../deliveryContext.tsx";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { FlexRowSpaceBetweenBox } from "../../../components/styledComponents.ts";
import { CancelButton } from "../../../components/buttons.tsx";

export const DeliveryValidationLoading = () => {
  const { t } = useTranslation();
  const { validationResponse, resetDelivery } = useContext(DeliveryContext);

  const getValidationKeysString = () => {
    if (!validationResponse?.validatorResults) return "";
    const keys = Object.keys(validationResponse.validatorResults);
    if (keys.length <= 1) return keys.join(", ");
    return keys.slice(0, -1).join(", ") + " " + t("and") + " " + keys[keys.length - 1];
  };

  return (
    <FlexRowSpaceBetweenBox>
      <Typography variant="body1">{t("validationIsRunning", { validators: getValidationKeysString() })}</Typography>
      <CancelButton onClick={() => resetDelivery()} />
    </FlexRowSpaceBetweenBox>
  );
};
