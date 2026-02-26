import { Typography } from "@mui/material";
import { FlexBox, FlexRowBox, FlexRowEndBox } from "../../../components/styledComponents";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { DeliveryContext } from "../deliveryContext";
import { useContext } from "react";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { ValidationStatus } from "../../../api/apiInterfaces";

export const DeliveryValidationResults = () => {
  const { jobId, validationResponse, resetDelivery } = useContext(DeliveryContext);

  const download = (fileName: string) => {
    if (!jobId) return;
    const url = `/api/v1/validation/${jobId}/files/${fileName}`;
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
  };

  return (
    <FlexBox>
      {validationResponse?.validatorResults &&
        Object.keys(validationResponse.validatorResults).map(key => (
          <FlexRowBox key={key} sx={{ alignItems: "start" }}>
            <Typography variant="h5" sx={{ fontStyle: "italic", margin: "0 20px 0 0" }}>
              {key}
            </Typography>
            <FlexBox>
              <Typography variant="body1">{validationResponse.validatorResults[key]?.statusMessage}</Typography>
              <FlexRowBox>
                {validationResponse.validatorResults[key]?.logFiles &&
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
            </FlexBox>
          </FlexRowBox>
        ))}
      {validationResponse?.status !== ValidationStatus.Completed && (
        <FlexRowEndBox>
          <CancelButton onClick={resetDelivery} />
        </FlexRowEndBox>
      )}
    </FlexBox>
  );
};
