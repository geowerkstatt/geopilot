import { FC } from "react";
import { Box, IconButton, LinearProgress, Stack, Typography } from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import { geopilotTheme } from "../appTheme";
import { FileUploadStatus } from "../pages/delivery/deliveryInterfaces.tsx";

const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
};

interface FileListItemProps {
  file: File;
  status?: FileUploadStatus;
  disabled?: boolean;
  onRemove: (file: File) => void;
}

export const FileListItem: FC<FileListItemProps> = ({ file, status, disabled, onRemove }) => {
  return (
    <Box
      sx={{
        border: `1px solid ${geopilotTheme.palette.primary.light}`,
        borderRadius: "4px",
        overflow: "hidden",
      }}>
      <Stack
        direction="row"
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "start",
          padding: "12px 16px",
        }}>
        <Stack spacing={0.5}>
          <Typography variant="body1" color="primary.main">
            {file.name}&nbsp;
            <Typography component="span" variant="caption" color="primary.light">
              ({formatFileSize(file.size)})
            </Typography>
          </Typography>
          {status?.state === "error" && status.error && (
            <Typography variant="body2" color="error">
              {status.error}
            </Typography>
          )}
        </Stack>
        <IconButton
          disabled={disabled}
          onClick={() => onRemove(file)}
          sx={{ color: geopilotTheme.palette.primary.main, padding: "0" }}>
          <ClearIcon />
        </IconButton>
      </Stack>
      {status?.state === "uploading" && <LinearProgress variant="indeterminate" />}
      {status?.state === "completed" && <LinearProgress variant="determinate" value={100} color="success" />}
      {status?.state === "error" && <LinearProgress variant="determinate" value={100} color="error" />}
    </Box>
  );
};
