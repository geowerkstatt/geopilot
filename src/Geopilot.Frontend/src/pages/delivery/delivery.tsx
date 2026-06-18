import { Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { CenteredBox } from "../../components/styledComponents.ts";
import { styled } from "@mui/system";
import { DeliveryStepper } from "./deliveryStepper.tsx";
import { DeliveryContentCarousel } from "./deliveryContentCarousel.tsx";

const DeliveryContainer = styled(Stack)(({ theme }) => ({
  flex: 1,
  alignItems: "flex-start",
  [theme.breakpoints.down("md")]: {
    alignItems: "stretch",
  },
}));

const Delivery = () => {
  const { t } = useTranslation();

  return (
    <CenteredBox data-cy="delivery">
      <Typography variant="h1" zIndex={10}>
        {t("deliveryTitle")}
      </Typography>
      <DeliveryContainer direction={{ xs: "column", md: "row" }} spacing={{ xs: 2, md: 2 }}>
        <DeliveryStepper />
        <DeliveryContentCarousel />
      </DeliveryContainer>
    </CenteredBox>
  );
};

export default Delivery;
