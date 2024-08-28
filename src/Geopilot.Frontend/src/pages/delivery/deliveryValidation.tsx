import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext, useEffect, useMemo } from "react";
import { FlexColumnBox, FlexRowBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { DeliveryStepEnum } from "./deliveryInterfaces.tsx";
import { BaseButton, CancelButton } from "../../components/buttons.tsx";

export const DeliveryValidation = () => {
  const { t } = useTranslation();
  const { isActiveStep, validationResponse, isLoading, validateFile, resetDelivery } = useContext(DeliveryContext);

  const isActive = useMemo(() => isActiveStep(DeliveryStepEnum.Validate), [isActiveStep]);

  useEffect(() => {
    validateFile();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const getValidationKeysString = () => {
    if (!validationResponse?.validatorResults) return "";
    const keys = Object.keys(validationResponse.validatorResults);
    if (keys.length <= 1) return keys.join(", ");
    return keys.slice(0, -1).join(", ") + " " + t("and") + " " + keys[keys.length - 1];
  };

  const download = (fileName: string) => {
    if (!validationResponse) return;
    const url = `/api/v1/validation/${validationResponse.jobId}/files/${fileName}`;
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
  };

  return (
    <FlexRowSpaceBetweenBox>
      {isLoading ? (
        <Typography variant="body1">{t("validationIsRunning", { validators: getValidationKeysString() })}</Typography>
      ) : (
        <FlexColumnBox>
          {validationResponse?.validatorResults &&
            Object.keys(validationResponse.validatorResults).map(key => (
              <FlexRowBox key={key} sx={{ alignItems: "start" }}>
                <Typography variant="h5" sx={{ fontStyle: "italic", margin: "0 20px 0 0" }}>
                  {key}
                </Typography>
                <FlexColumnBox>
                  <Typography variant="body1">{validationResponse.validatorResults[key].statusMessage}</Typography>
                  <FlexRowBox>
                    {validationResponse.validatorResults[key].logFiles &&
                      Object.keys(validationResponse.validatorResults[key].logFiles).map((logFileKey, index) => (
                        <BaseButton
                          key={index}
                          variant="outlined"
                          onClick={() => {
                            download(validationResponse.validatorResults[key].logFiles[logFileKey]);
                          }}
                          icon={<FileDownloadIcon />}
                          label={logFileKey}
                        />
                      ))}
                  </FlexRowBox>
                </FlexColumnBox>
              </FlexRowBox>
            ))}
        </FlexColumnBox>
      )}
      {isActive && <CancelButton onClick={() => resetDelivery()} />}
    </FlexRowSpaceBetweenBox>
  );
};
