import { useContext } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import i18next from "i18next";
import { DeliveryContext } from "../deliveryContext.tsx";

const localized = (entries?: Record<string, string>) =>
  entries?.[i18next.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "";

export const DeliveryProcessingLoading = () => {
  const { t } = useTranslation();
  const { processingResponse } = useContext(DeliveryContext);

  const pipelineName = localized(processingResponse?.pipelineName);
  const message = pipelineName ? t("processingIsRunning", { pipeline: pipelineName }) : t("processingIsBeingPrepared");

  return (
    <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "space-between" }}>
      <Typography variant="body1">{message}</Typography>
    </Stack>
  );
};
