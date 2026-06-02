import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox, GeopilotBox } from "../../components/styledComponents.ts";
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

const DeliveryContentBox = styled(GeopilotBox)({
  flex: 1,
});

const Delivery = () => {
  const { t } = useTranslation();
  const { steps, activeStep } = useContext(DeliveryContext);

  return (
    <CenteredBox data-cy="delivery" sx={{ height: "auto" }}>
      <Typography variant="h1">{t("deliveryTitle")}</Typography>
      <DeliveryContainer direction={{ xs: "column", md: "row" }} spacing={{ xs: 2, md: 3 }}>
        <DeliveryStepper />
        <DeliveryContentBox>{Array.from(steps.values())[activeStep]?.content}</DeliveryContentBox>
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
