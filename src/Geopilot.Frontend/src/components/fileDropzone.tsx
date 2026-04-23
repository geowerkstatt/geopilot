import { CSSProperties, FC, useCallback, useEffect, useMemo, useState } from "react";
import { Link, Typography } from "@mui/material";
import { FileRejection, useDropzone } from "react-dropzone";
import { useTranslation } from "react-i18next";
import { geopilotTheme } from "../appTheme";
import { FlexBox } from "./styledComponents.ts";
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
  setFileError: (error: string | undefined) => void;
  maxFileSizeMB?: number;
  maxFiles?: number;
  isUploading?: boolean;
}

export const FileDropzone: FC<FileDropzoneProps> = ({
  selectedFiles,
  addFiles,
  removeFile,
  fileUploadStatus,
  fileExtensions,
  disabled,
  setFileError,
  maxFileSizeMB = defaultMaxFileSizeMB,
  maxFiles = 1,
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

  const getAcceptedFileTypesText = useCallback(() => {
    return acceptsAllFileTypes
      ? ""
      : (fileExtensions?.length ?? 0) > 1
        ? `${fileExtensions!.slice(0, -1).join(", ")} ${t("or")} ${fileExtensions!.slice(-1)}`
        : (fileExtensions?.join(", ") ?? "");
  }, [acceptsAllFileTypes, fileExtensions, t]);

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
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      minHeight: "56px",
      padding: "20px",
      border: `2px dashed`,
      borderColor: disabled
        ? geopilotTheme.palette.primary.inactive
        : error
          ? geopilotTheme.palette.error.main
          : geopilotTheme.palette.primary.main,
      borderRadius: "4px",
      backgroundColor: error ? geopilotTheme.palette.error.hover : geopilotTheme.palette.primary.hover,
      outline: "none",
      transition: "border .24s ease-in-out",
      cursor: disabled ? "default" : "pointer",
    }),
    [disabled, error],
  );

  return (
    <FlexBox>
      <div {...getRootProps({ style: dropzoneStyle })}>
        <input {...getInputProps()} data-cy="file-dropzone" />
        <Typography variant="body1" className={disabled ? "Mui-disabled" : ""}>
          <Link>{t("clickToSelect")}</Link>
          &nbsp;
          {t("or")} {t("dragAndDrop")}
        </Typography>
        {fileExtensions && fileExtensions.length > 0 && (
          <Typography variant="caption" className={disabled ? "Mui-disabled" : ""}>
            {getAcceptedFileTypesText()}&nbsp;(max.{" "}
            {maxFileSizeMB >= 1024 ? `${(maxFileSizeMB / 1024).toFixed(0)} GB` : `${maxFileSizeMB} MB`})
          </Typography>
        )}
      </div>
      {selectedFiles.map(file => (
        <FileListItem
          key={file.name}
          file={file}
          status={fileUploadStatus.get(file.name)}
          disabled={isUploading}
          onRemove={removeFile}
        />
      ))}
    </FlexBox>
  );
};
