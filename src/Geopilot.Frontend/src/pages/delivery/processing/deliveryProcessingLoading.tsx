import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { DeliveryContext } from "../deliveryContext.tsx";

export const DeliveryProcessingLoading = () => {
  const { t } = useTranslation();
  const localized = useLocalized();
  const { processingResponse } = useContext(DeliveryContext);

  const pipelineName = localized(processingResponse?.pipelineName);
  const message = pipelineName ? t("processingIsRunning", { pipeline: pipelineName }) : t("processingIsBeingPrepared");

  return (
    <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "space-between" }}>
      <Typography variant="body1">{message}</Typography>
    </Stack>
  );
};
