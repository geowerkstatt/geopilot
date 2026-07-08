import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { DeliveryGrid } from "../../../components/grids/deliveryGrid.tsx";
import { CenteredContent } from "../../../components/styledComponents.ts";

export const UserDeliveryOverview = () => {
  const { t } = useTranslation();

  return (
    <CenteredContent sx={{ height: "100%" }}>
      <Typography variant="h2">{t("myDeliveries")}</Typography>
      <DeliveryGrid fetchUrl="/api/v1/delivery/uploads" columns={["date", "mandateName", "comment"]} />
    </CenteredContent>
  );
};
