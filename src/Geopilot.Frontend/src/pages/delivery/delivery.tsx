import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox, GeopilotBox } from "../../components/styledComponents.ts";
import { styled } from "@mui/system";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepper } from "./deliveryStepper.tsx";
import { useContext } from "react";

const DeliveryContainer = styled(Stack)({
  flex: 1,
});

const DeliveryContentBox = styled(GeopilotBox)({
  flex: 1,
});

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep } = useContext(DeliveryContext);

  return (
    <CenteredBox data-cy="delivery">
      <Typography variant="h1">{t("deliveryTitle")}</Typography>
      <DeliveryContainer direction={{ sm: "column", md: "row" }} spacing={3} alignItems="flex-start">
        <DeliveryStepper />
        <DeliveryContentBox>{Array.from(steps.values())[activeStep]?.content}</DeliveryContentBox>
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
