import { CSSProperties, FC, useCallback, useEffect, useMemo, useState } from "react";
import { IconButton, Link, Typography } from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import { FileRejection, useDropzone } from "react-dropzone";
import { useTranslation } from "react-i18next";
import { geopilotTheme } from "../appTheme";
import { FlexRowBox } from "./styledComponents.ts";

interface FileDropzoneProps {
  selectedFile?: File;
  setSelectedFile: (file: File | undefined) => void;
  fileExtensions?: string[];
  disabled?: boolean;
  setFileError: (error: string | undefined) => void;
}

export const FileDropzone: FC<FileDropzoneProps> = ({
  selectedFile,
  setSelectedFile,
  fileExtensions,
  disabled,
  setFileError,
}) => {
  const { t } = useTranslation();
  const [acceptsAllFileTypes, setAcceptsAllFileTypes] = useState<boolean>(true);
  const [error, setError] = useState<string>();

  useEffect(() => {
    setFileError(error);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [error]);

  useEffect(() => {
    setAcceptsAllFileTypes(!fileExtensions || fileExtensions?.includes(".*"));
  }, [fileExtensions]);

  const getAcceptedFileTypesText = useCallback(() => {
    return acceptsAllFileTypes
      ? ""
      : (fileExtensions?.length ?? 0) > 1
        ? `${fileExtensions!.slice(0, -1).join(", ")} ${t("or")} ${fileExtensions!.slice(-1)}`
        : fileExtensions?.join(", ") ?? "";
  }, [acceptsAllFileTypes, fileExtensions, t]);

  const onDrop = useCallback(
    (acceptedFiles: File[], fileRejections: FileRejection[]) => {
      if (error) {
        setError(undefined);
      }
      if (fileRejections.length > 0) {
        let errorMessage: string;
        const errorCode = fileRejections[0].errors[0].code;

        switch (errorCode) {
          case "file-invalid-type":
            errorMessage = t("fileDropzoneErrorNotSupported");
            break;
          case "too-many-files":
            errorMessage = t("fileDropzoneErrorTooManyFiles");
            break;
          case "file-too-large":
            errorMessage = t("fileDropzoneErrorFileTooLarge");
            break;
          default:
            errorMessage = t("fileDropzoneErrorChooseFile");
            break;
        }

        setError(errorMessage);
      } else {
        setSelectedFile(acceptedFiles[0]);
      }
    },
    [error, t, setSelectedFile],
  );

  const { getRootProps, getInputProps } = useDropzone({
    onDrop,
    maxFiles: 1,
    maxSize: 209715200,
    accept: acceptsAllFileTypes
      ? undefined
      : {
          "application/x-geopilot-files": fileExtensions ?? [],
        },
    disabled,
  });

  const handleRemove = () => {
    if (!disabled) {
      setSelectedFile(undefined);
    }
  };

  const style = useMemo<CSSProperties>(
    () => ({
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      height: "100px",
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
    <div {...getRootProps({ style })}>
      <input {...getInputProps()} />
      {!selectedFile ? (
        <>
          <Typography variant="body1" className={disabled ? "Mui-disabled" : ""}>
            <Link>{t("clickToUpload")}</Link>
            &nbsp;
            {t("or")} {t("dragAndDrop")}
          </Typography>
          {fileExtensions && fileExtensions.length > 0 && (
            <Typography variant="caption" className={disabled ? "Mui-disabled" : ""}>
              {getAcceptedFileTypesText()}&nbsp;(max. 200 MB)
            </Typography>
          )}
        </>
      ) : (
        <FlexRowBox>
          <Typography
            variant="body1"
            sx={{ color: geopilotTheme.palette.primary.main }}
            className={disabled ? "Mui-disabled" : ""}>
            {selectedFile?.name}
          </Typography>
          <IconButton
            disabled={disabled}
            onClick={e => {
              e.stopPropagation();
              handleRemove();
            }}>
            <ClearIcon />
          </IconButton>
        </FlexRowBox>
      )}
    </div>
  );
};
