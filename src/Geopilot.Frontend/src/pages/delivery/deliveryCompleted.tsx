import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";
import { FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import { BaseButton } from "../../components/buttons.tsx";

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
