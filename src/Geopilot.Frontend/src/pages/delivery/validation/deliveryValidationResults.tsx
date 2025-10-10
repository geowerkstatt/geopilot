import { Accordion, AccordionDetails, AccordionSummary, styled, Typography } from "@mui/material";
import { FlexBox, FlexRowBox, FlexRowEndBox } from "../../../components/styledComponents";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { DeliveryContext } from "../deliveryContext";
import { useContext } from "react";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { ValidationStatus } from "../../../api/apiInterfaces";
import { ValidationResultsTree } from "../validationResultsTree";
import { ValidationVisualisation } from "../validationVisualisation";
import { useTranslation } from "react-i18next";

const StyledAccordion = styled(Accordion)(({ theme }) => ({
  backgroundColor: "transparent",
  border: `1px solid ${theme.palette.divider}`,
  borderRadius: theme.shape.borderRadius,
  boxShadow: "none",
  "&:before": { display: "none" }, // remove default divider line
  "&.Mui-expanded": {
    margin: `${theme.spacing(0.5)} 0`,
  },
}));

export const DeliveryValidationResults = () => {
  const { t } = useTranslation();
  const { validationResponse, resetDelivery, selectedFile } = useContext(DeliveryContext);

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
    <FlexBox>
      {validationResponse?.validatorResults &&
        Object.keys(validationResponse.validatorResults).map(key => (
          <FlexRowBox key={key} sx={{ alignItems: "start" }}>
            <Typography variant="h5" sx={{ fontStyle: "italic", margin: "0 20px 0 0" }}>
              {key}
            </Typography>
            <FlexBox sx={{ flex: 1, gap: 1 }}>
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
              {key === "INTERLIS" && selectedFile?.name === "DMAVTYM_Alles_V1_0_buggy2.xtf" && (
                <>
                  <StyledAccordion>
                    <AccordionSummary expandIcon={<ExpandMoreIcon />}>Logs</AccordionSummary>
                    <AccordionDetails>
                      <ValidationResultsTree />
                    </AccordionDetails>
                  </StyledAccordion>
                  <StyledAccordion>
                    <AccordionSummary expandIcon={<ExpandMoreIcon />}>{t("validationVisualisation")}</AccordionSummary>
                    <AccordionDetails>
                      <ValidationVisualisation />
                    </AccordionDetails>
                  </StyledAccordion>
                </>
              )}
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
