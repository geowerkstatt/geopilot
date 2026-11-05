import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack, TableCell, TableRow, Typography } from "@mui/material";
import { PipelineRunFileResponse, ScanState } from "../../../api/generated";
import { DetailTable, nameColumnWidth } from "../../../components/detailTable.tsx";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { formatFileSize } from "./processingRunFormat.ts";
import { TechnicalText } from "./technicalText.tsx";

interface ProcessingRunFilesProps {
  files: PipelineRunFileResponse[];
  scanState: ScanState;
  scanDetails?: string | null;
}

/** The files uploaded for a run with their size and hash, and what the virus scan found. */
export const ProcessingRunFiles: FC<ProcessingRunFilesProps> = ({ files, scanState, scanDetails }) => {
  const { t } = useTranslation();

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
    <Stack data-cy="processingRunFiles">
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
    </Stack>
  );
};
