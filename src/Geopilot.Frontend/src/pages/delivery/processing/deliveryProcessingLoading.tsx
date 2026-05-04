import { useContext } from "react";
import i18next from "i18next";
import { DeliveryContext } from "../deliveryContext.tsx";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { FlexRowSpaceBetweenBox } from "../../../components/styledComponents.ts";
import { CancelButton } from "../../../components/buttons.tsx";

const localized = (entries?: Record<string, string>) =>
  entries?.[i18next.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "";

export const DeliveryProcessingLoading = () => {
  const { t } = useTranslation();
  const { processingResponse, resetDelivery } = useContext(DeliveryContext);

  const pipelineName = localized(processingResponse?.pipelineName);
  const message = pipelineName ? t("processingIsRunning", { pipeline: pipelineName }) : t("processingIsBeingPrepared");

  return (
    <FlexRowSpaceBetweenBox>
      <Typography variant="body1">{message}</Typography>
      <CancelButton onClick={() => resetDelivery()} />
    </FlexRowSpaceBetweenBox>
  );
};
