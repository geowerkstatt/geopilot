import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { styled } from "@mui/system";
import { DeliveryGrid } from "../../../components/grids/deliveryGrid.tsx";
import { CenteredBox } from "../../../components/styledComponents.ts";

const DeliveryOverviewBox = styled(CenteredBox)({
  height: "100%",
});

export const UserDeliveryOverview = () => {
  const { t } = useTranslation();

  return (
    <DeliveryOverviewBox>
      <Typography variant="h2">{t("myDeliveries")}</Typography>
      <DeliveryGrid fetchUrl="/api/v1/delivery/uploads" columns={["date", "mandateName", "comment"]} />
    </DeliveryOverviewBox>
  );
};
