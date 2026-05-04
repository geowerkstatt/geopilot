import { Typography } from "@mui/material";
import { FlexBox, FlexRowBox, FlexRowEndBox } from "../../../components/styledComponents";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { DeliveryContext } from "../deliveryContext";
import { useContext } from "react";
import i18next from "i18next";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { isProcessingDeliverable } from "../deliveryUtils";

const localized = (entries?: Record<string, string>) =>
  entries?.[i18next.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "";

export const DeliveryProcessingResults = () => {
  const { processingResponse, resetDelivery, isProcessing } = useContext(DeliveryContext);

  const download = (url: string) => {
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.click();
  };

  return (
    <FlexBox>
      {processingResponse?.deliveryRestrictionMessage && (
        <Typography variant="body1" color="error">
          {localized(processingResponse.deliveryRestrictionMessage)}
        </Typography>
      )}
      {processingResponse?.steps?.map(step => (
        <FlexRowBox key={step.id} sx={{ alignItems: "start" }}>
          <Typography variant="h5" sx={{ fontStyle: "italic", margin: "0 20px 0 0", minWidth: "220px", flexShrink: 0 }}>
            {localized(step.name)}
          </Typography>
          <FlexBox>
            {step.statusMessage && <Typography variant="body1">{localized(step.statusMessage)}</Typography>}
            {Object.keys(step.downloads).length > 0 && (
              <FlexRowBox>
                {Object.entries(step.downloads).map(([key, url]) => (
                  <BaseButton
                    key={key}
                    variant="outlined"
                    onClick={() => download(url)}
                    icon={<FileDownloadIcon />}
                    label={key}
                  />
                ))}
              </FlexRowBox>
            )}
          </FlexBox>
        </FlexRowBox>
      ))}
      {!isProcessing && !isProcessingDeliverable(processingResponse) && (
        <FlexRowEndBox>
          <CancelButton onClick={resetDelivery} />
        </FlexRowEndBox>
      )}
    </FlexBox>
  );
};
