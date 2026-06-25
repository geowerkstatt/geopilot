import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { FlexRowSpaceBetweenBox } from "../../../components/styledComponents.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { DeliveryContext } from "../deliveryContext.tsx";

export const DeliveryProcessingLoading = () => {
  const { t } = useTranslation();
  const localized = useLocalized();
  const { processingResponse } = useContext(DeliveryContext);

  const pipelineName = localized(processingResponse?.pipelineName);
  const message = pipelineName ? t("processingIsRunning", { pipeline: pipelineName }) : t("processingIsBeingPrepared");

  return (
    <FlexRowSpaceBetweenBox>
      <Typography variant="body1">{message}</Typography>
    </FlexRowSpaceBetweenBox>
  );
};
