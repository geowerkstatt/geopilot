import { CSSProperties, FC, useCallback, useEffect, useMemo, useState } from "react";
import { FileRejection, useDropzone } from "react-dropzone";
import { useTranslation } from "react-i18next";
import { Box, Link, Stack, Typography } from "@mui/material";
import { geopilotTheme } from "../appTheme";
import { FileUploadStatus } from "../pages/delivery/deliveryInterfaces.tsx";
import { FileListItem } from "./fileListItem.tsx";

const defaultMaxFileSizeMB = 100;

interface FileDropzoneProps {
  selectedFiles: File[];
  addFiles: (files: File[]) => void;
  removeFile: (file: File) => void;
  fileUploadStatus: Map<string, FileUploadStatus>;
  fileExtensions?: string[];
  disabled?: boolean;
  hideDropzone?: boolean;
  setFileError: (error: string | undefined) => void;
  maxFileSizeMB?: number;
  maxFiles?: number;
  maxTotalFileSizeMB?: number;
  isUploading?: boolean;
}

export const FileDropzone: FC<FileDropzoneProps> = ({
  selectedFiles,
  addFiles,
  removeFile,
  fileUploadStatus,
  fileExtensions,
  disabled,
  hideDropzone,
  setFileError,
  maxFileSizeMB = defaultMaxFileSizeMB,
  maxFiles = 1,
  maxTotalFileSizeMB = defaultMaxFileSizeMB,
  isUploading,
}) => {
  const { t } = useTranslation();
  const [acceptsAllFileTypes, setAcceptsAllFileTypes] = useState<boolean>(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    setFileError(error);
  }, [error, setFileError]);

  useEffect(() => {
    setAcceptsAllFileTypes(!fileExtensions || fileExtensions?.includes(".*"));
  }, [fileExtensions]);

  const onDrop = useCallback(
    (acceptedFiles: File[], fileRejections: FileRejection[]) => {
      if (error) {
        setError(undefined);
      }
      if (fileRejections.length > 0) {
        let errorKey: string;
        const errorCode = fileRejections[0].errors[0].code;

        switch (errorCode) {
          case "file-invalid-type":
            errorKey = "fileDropzoneErrorNotSupported";
            break;
          case "too-many-files":
            errorKey = "fileDropzoneErrorTooManyFiles";
            break;
          case "file-too-large":
            errorKey = "fileDropzoneErrorFileTooLarge";
            break;
          default:
            errorKey = "fileDropzoneErrorChooseFile";
            break;
        }

        setError(errorKey);
      } else if (acceptedFiles.length > 0) {
        const existingNames = new Set(selectedFiles.map(f => f.name));
        const uniqueNewFiles = acceptedFiles.filter(f => !existingNames.has(f.name));
        if (selectedFiles.length + uniqueNewFiles.length > maxFiles) {
          setError("fileDropzoneErrorTooManyFiles");
        } else {
          addFiles(acceptedFiles);
        }
      }
    },
    [error, addFiles, selectedFiles, maxFiles],
  );

  const { getRootProps, getInputProps } = useDropzone({
    onDrop,
    maxFiles,
    maxSize: maxFileSizeMB * 1024 * 1024,
    accept: acceptsAllFileTypes
      ? undefined
      : {
          "application/x-geopilot-files": fileExtensions ?? [],
        },
    disabled,
  });

  const dropzoneStyle = useMemo<CSSProperties>(
    () => ({
      display: hideDropzone ? "none" : "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      minHeight: "56px",
      padding: geopilotTheme.spacing(3),
      border: `2px dashed`,
      borderColor: disabled
        ? geopilotTheme.palette.primary.states.disabledBackground
        : error
          ? geopilotTheme.palette.error.main
          : geopilotTheme.palette.primary.main,
      borderRadius: "4px",
      backgroundColor: error ? geopilotTheme.palette.error.hover : geopilotTheme.palette.primary.states.hover,
      outline: "none",
      transition: "border .24s ease-in-out",
      cursor: disabled ? "default" : "pointer",
    }),
    [disabled, hideDropzone, error],
  );

  const formatMB = (sizeMB: number) => (sizeMB >= 1024 ? `${(sizeMB / 1024).toFixed(0)} GB` : `${sizeMB} MB`);
  const fileCountText = t("maxFileCount", { count: maxFiles });
  const maxPerFileText = t("maxPerFile", { size: formatMB(maxFileSizeMB) });
  const maxTotalSizeText = t("maxTotalSize", { size: formatMB(maxTotalFileSizeMB) });

  return (
    <Stack>
      <div {...getRootProps({ style: dropzoneStyle })} data-cy="file-dropzone">
        <input {...getInputProps()} />
        <Typography variant="body1" color="text.primary" className={disabled ? "Mui-disabled" : ""}>
          <Link>{t("clickToSelect")}</Link>
          &nbsp;
          {t("or")} {t("dragAndDrop")}
        </Typography>
        {fileExtensions && fileExtensions.length > 0 && (
          <Typography variant="caption" color="text.secondary" className={disabled ? "Mui-disabled" : ""}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              alignItems={"center"}
              gap={{ xs: 0, sm: 1 }}
              divider={
                <Box component="span" sx={{ display: { xs: "none", sm: "inline" } }}>
                  |
                </Box>
              }>
              <span>{fileCountText}</span>
              <span>{maxPerFileText}</span>
              <span>{maxTotalSizeText}</span>
            </Stack>
          </Typography>
        )}
      </div>
      {selectedFiles.map(file => (
        <FileListItem
          key={file.name}
          file={file}
          status={fileUploadStatus.get(file.name)}
          disabled={disabled || isUploading}
          onRemove={removeFile}
        />
      ))}
    </Stack>
  );
};
