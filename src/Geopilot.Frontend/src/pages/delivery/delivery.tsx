import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox } from "../../components/styledComponents.ts";
import { styled } from "@mui/system";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepper } from "./deliveryStepper.tsx";
import { useContext } from "react";

const DeliveryContainer = styled(Stack)(({ theme }) => ({
  flex: 1,
  alignItems: "flex-start",
  [theme.breakpoints.down("md")]: {
    alignItems: "stretch",
  },
}));

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep, lastCompletedStep } = useContext(DeliveryContext);
  const isCompleted = lastCompletedStep >= activeStep;

  return (
    <CenteredBox data-cy="delivery">
      <Typography variant="h1" zIndex={10}>
        {t("deliveryTitle")}
      </Typography>
      <DeliveryContainer direction={{ xs: "column", md: "row" }} spacing={{ xs: 2, md: 3 }}>
        <DeliveryStepper />
        {Array.from(steps.values())[activeStep]?.content(isCompleted)}
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
