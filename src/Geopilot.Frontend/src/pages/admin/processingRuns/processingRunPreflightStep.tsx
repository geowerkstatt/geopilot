import { FC } from "react";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Accordion, AccordionDetails, AccordionSummary, Stack, TableCell, TableRow, Typography } from "@mui/material";
import { PipelineRunResponse, ScanState } from "../../../api/generated";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { StepIcon } from "../../../components/stepIcon.tsx";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatFileSize, formatStepState, preflightStepState } from "./processingRunFormat.ts";
import { TechnicalText } from "./technicalText.tsx";

interface ProcessingRunPreflightStepProps {
  run: PipelineRunResponse;
}

/**
 * The preflight of a run as the first step, named as in the delivery: the uploaded files with their size and hash,
 * what the virus scan found, and why the preflight failed.
 */
export const ProcessingRunPreflightStep: FC<ProcessingRunPreflightStepProps> = ({ run }) => {
  const { t } = useTranslation();
  const state = preflightStepState(run);
  const { files, scanState, scanDetails } = run;

  const scanResult = () => {
    switch (scanState) {
      case ScanState.Clean:
        return t("scanResultClean", { count: files.length });
      case ScanState.NotScanned:
        return t("scanResultNotScanned", { count: files.length });
      case ScanState.ThreatDetected:
        return scanDetails
          ? t("scanResultThreatDetectedWithDetails", { details: scanDetails })
          : t("scanResultThreatDetected");
    }
  };

  return (
    <Accordion defaultExpanded disableGutters data-cy="processingRunStep-preflight">
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
          <StepIcon step={1} state={state} variant="outlined" />
          <Typography sx={{ flex: "1 1 0" }}>{t("preparation")}</Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {formatStepState(t, state)}
          </Typography>
        </Stack>
      </AccordionSummary>
      <AccordionDetails>
        <Stack>
          <SummaryField labelVariant="h5" label="uploadedFiles">
            <DetailTable
              columns={[
                { label: t("fileName"), width: nameColumnWidth },
                { label: t("fileSize"), width: "12%" },
                { label: t("sha256") },
              ]}>
              {files.map(file => (
                <TableRow key={file.storageKey}>
                  <TableCell>{file.fileName}</TableCell>
                  <TableCell>{formatFileSize(file.declaredSize)}</TableCell>
                  <TableCell sx={{ color: file.sha256 ? undefined : "text.secondary" }}>
                    {file.sha256 ? <TechnicalText>{file.sha256}</TechnicalText> : t("hashNotComputed")}
                  </TableCell>
                </TableRow>
              ))}
            </DetailTable>
          </SummaryField>
          <Typography>{scanResult()}</Typography>
          {run.outcome.preflightFailed && run.failureReason && (
            <SummaryField labelVariant="h5" label="errorMessage">
              {run.failureReason}
            </SummaryField>
          )}
        </Stack>
      </AccordionDetails>
    </Accordion>
  );
};
