import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";
import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { BaseButton } from "../../components/buttons.tsx";
import { DeliveryContent } from "./deliveryContent.tsx";

export const DeliveryCompleted = () => {
  const { t } = useTranslation();
  const { submittedData, resetDelivery } = useContext(DeliveryContext);

  const textKey = submittedData ? "deliveryCompleted" : "validationCompleted";
  const buttonLabelKey = submittedData ? "addAnotherDelivery" : "validateAnotherFile";

  const button = (
    <BaseButton
      onClick={() => resetDelivery()}
      variant="outlined"
      label={buttonLabelKey}
      sx={{ backgroundColor: "white" }}
    />
  );

  return (
    <DeliveryContent title="done" buttons={button}>
      <Typography variant="body1">{t(textKey)}</Typography>
    </DeliveryContent>
  );
};
