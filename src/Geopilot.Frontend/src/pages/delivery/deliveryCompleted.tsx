import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";
import { FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import { BaseButton } from "../../components/buttons.tsx";
import { useGeopilotAuth } from "../../auth/index.ts";

export const DeliveryCompleted = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { resetDelivery } = useContext(DeliveryContext);

  if (user) {
    return (
      <FlexRowSpaceBetweenBox>
        <Typography variant="body1">{t("deliveryCompleted")}</Typography>
        <BaseButton onClick={() => resetDelivery()} icon={<AddIcon />} variant="outlined" label="addAnotherDelivery" />
      </FlexRowSpaceBetweenBox>
    );
  } else {
    return (
      <FlexRowSpaceBetweenBox>
        <Typography variant="body1">{t("validationCompleted")}</Typography>
        <BaseButton onClick={() => resetDelivery()} icon={<AddIcon />} variant="outlined" label="validateAnotherFile" />
      </FlexRowSpaceBetweenBox>
    );
  }
};
