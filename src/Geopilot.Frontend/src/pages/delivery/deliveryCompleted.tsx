import { useContext } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import { Typography } from "@mui/material";
import { BaseButton } from "../../components/buttons.tsx";
import { FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { DeliveryContext } from "./deliveryContext.tsx";

export const DeliveryCompleted = () => {
  const { t } = useTranslation();
  const { resetDelivery } = useContext(DeliveryContext);

  return (
    <FlexRowSpaceBetweenBox>
      <Typography variant="body1">{t("deliveryCompleted")}</Typography>
      <BaseButton onClick={() => resetDelivery()} icon={<AddIcon />} variant="outlined" label="addAnotherDelivery" />
    </FlexRowSpaceBetweenBox>
  );
};
