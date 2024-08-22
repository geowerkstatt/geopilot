import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext, useEffect } from "react";
import { FlexColumnBox, FlexRowBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Button, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";

export const DeliveryValidation = () => {
  const { t } = useTranslation();
  const { jobId, validationResults, isLoading, validateFile, resetDelivery } = useContext(DeliveryContext);

  useEffect(() => {
    validateFile();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const getValidationKeysString = () => {
    if (!validationResults) return "";
    const keys = Object.keys(validationResults);
    if (keys.length <= 1) return keys.join(", ");
    return keys.slice(0, -1).join(", ") + " " + t("and") + " " + keys[keys.length - 1];
  };

  const download = (fileName: string) => {
    const url = `/api/v1/validation/${jobId}/files/${fileName}`;
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
  };

  return (
    <FlexColumnBox>
      {isLoading ? (
        <FlexRowSpaceBetweenBox>
          <Typography variant="body1">{t("validationIsRunning", { validators: getValidationKeysString() })}</Typography>
          <Button variant="outlined" startIcon={<CancelOutlinedIcon />} onClick={() => resetDelivery()}>
            {t("cancel")}
          </Button>
        </FlexRowSpaceBetweenBox>
      ) : (
        validationResults &&
        Object.keys(validationResults).map(key => (
          <FlexRowBox key={key} sx={{ alignItems: "start" }}>
            <Typography variant="h5" sx={{ fontStyle: "italic", margin: "0 20px 0 0" }}>
              {key}
            </Typography>
            <FlexColumnBox>
              <Typography variant="body1">{validationResults[key].statusMessage}</Typography>
              <FlexRowBox>
                {validationResults[key].logFiles &&
                  Object.keys(validationResults[key].logFiles).map((logFileKey, index) => (
                    <Button
                      key={index}
                      variant="outlined"
                      color="primary"
                      startIcon={<FileDownloadIcon />}
                      onClick={() => {
                        download(validationResults[key].logFiles[logFileKey]);
                      }}>
                      {logFileKey}
                    </Button>
                  ))}
              </FlexRowBox>
            </FlexColumnBox>
          </FlexRowBox>
        ))
      )}
    </FlexColumnBox>
  );
};
