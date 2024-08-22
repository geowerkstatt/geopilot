import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext } from "react";
import { FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Button, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";

export const DeliveryCompleted = () => {
  const { t } = useTranslation();
  const { resetDelivery } = useContext(DeliveryContext);

  return (
    <FlexRowSpaceBetweenBox>
      <Typography variant="body1">{t("deliveryCompleted")}</Typography>
      <Button
        variant="outlined"
        color="primary"
        startIcon={<AddIcon />}
        onClick={() => {
          resetDelivery();
        }}>
        {t("addAnotherDelivery")}
      </Button>
    </FlexRowSpaceBetweenBox>
  );
};
