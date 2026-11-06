import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { PipelineRunFileResponse, ScanState } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { formatFileSize } from "./pipelineRunFormat.ts";
import { TechnicalText } from "./technicalText.tsx";

interface PipelineRunFilesProps {
  files: PipelineRunFileResponse[];
  scanState: ScanState;
  scanDetails?: string | null;
}

const scanStateKey: Record<ScanState, TranslationKey> = {
  [ScanState.NotScanned]: "scanStateNotScanned",
  [ScanState.Clean]: "scanStateClean",
  [ScanState.ThreatDetected]: "scanStateThreatDetected",
};

/** The files uploaded for a run with their size and hash, and what the virus scan found. */
export const PipelineRunFiles: FC<PipelineRunFilesProps> = ({ files, scanState, scanDetails }) => {
  const { t } = useTranslation();

  return (
    <Stack sx={{ gap: 2 }} data-cy="pipelineRunFiles">
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t("fileName")}</TableCell>
            <TableCell>{t("fileSize")}</TableCell>
            <TableCell>{t("sha256")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {files.map(file => (
            <TableRow key={file.storageKey}>
              <TableCell sx={{ overflowWrap: "anywhere" }}>
                <TechnicalText>{file.fileName}</TechnicalText>
              </TableCell>
              <TableCell>{formatFileSize(file.declaredSize)}</TableCell>
              <TableCell sx={{ overflowWrap: "anywhere", color: file.sha256 ? undefined : "text.secondary" }}>
                {file.sha256 ? <TechnicalText>{file.sha256}</TechnicalText> : t("hashNotComputed")}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      <Typography variant="body2">
        {`${t("virusScan")}: ${t(scanStateKey[scanState])}`}
        {scanDetails && ` (${scanDetails})`}
      </Typography>
    </Stack>
  );
};
