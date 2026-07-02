import { FC } from "react";
import ClearIcon from "@mui/icons-material/Clear";
import { Box, LinearProgress, Stack, Typography } from "@mui/material";
import { geopilotTheme } from "../appTheme";
import { FileUploadStatus } from "../pages/delivery/deliveryInterfaces.tsx";
import { IconButton } from "./buttons";

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
      }}
      data-cy="file-list-item">
      <Stack
        direction="row"
        sx={{
          alignItems: "center",
          flexWrap: "wrap",
          px: 2,
          pt: 1.5,
          pb: status?.state !== "neutral" ? 1 : 1.5,
          justifyContent: "space-between",
        }}>
        <Stack gap={0.5}>
          <Typography variant="body1" color="primary.main">
            {file.name}&nbsp;
            <Typography component="span" variant="caption" color="primary.light" sx={{ verticalAlign: "middle" }}>
              ({formatFileSize(file.size)})
            </Typography>
          </Typography>
          {status?.state === "error" && status.error && (
            <Typography variant="body2" color="error">
              {status.error}
            </Typography>
          )}
        </Stack>
        {!disabled && (
          <IconButton onClick={() => onRemove(file)} sx={{ padding: "0" }}>
            <ClearIcon />
          </IconButton>
        )}
      </Stack>
      {status?.state === "uploading" && <LinearProgress variant="indeterminate" />}
      {status?.state === "completed" && !disabled && (
        <LinearProgress variant="determinate" value={100} color="success" />
      )}
      {status?.state === "error" && <LinearProgress variant="determinate" value={100} color="error" />}
    </Box>
  );
};
