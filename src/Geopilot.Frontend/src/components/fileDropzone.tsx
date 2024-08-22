import { CSSProperties, FC, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { IconButton, Link, Typography } from "@mui/material";
import ClearIcon from "@mui/icons-material/Clear";
import { FileRejection, useDropzone } from "react-dropzone";
import { useTranslation } from "react-i18next";
import { geopilotTheme } from "../appTheme";
import { FlexRowBox } from "./styledComponents.ts";
import { AlertContext } from "./alert/alertContext.tsx";

interface FileDropzoneProps {
  selectedFile?: File;
  setSelectedFile: (file: File | undefined) => void;
  fileExtensions?: string[];
  disabled?: boolean;
}

export const FileDropzone: FC<FileDropzoneProps> = ({ selectedFile, setSelectedFile, fileExtensions, disabled }) => {
  const { t } = useTranslation();
  const [acceptsAllFileTypes, setAcceptsAllFileTypes] = useState<boolean>(true);
  const { showAlert } = useContext(AlertContext);

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
      if (fileRejections.length > 0) {
        let errorMessage: string;
        const errorCode = fileRejections[0].errors[0].code;
        const genericError = acceptsAllFileTypes
          ? t("fileDropzoneErrorChooseFile")
          : t("fileDropzoneErrorChooseFileOfType", { acceptedFileTypesText: getAcceptedFileTypesText() });

        switch (errorCode) {
          case "file-invalid-type":
            errorMessage = t("fileDropzoneErrorNotSupported", { genericError: genericError });
            break;
          case "too-many-files":
            errorMessage = t("fileDropzoneErrorTooManyFiles");
            break;
          case "file-too-large":
            errorMessage = t("fileDropzoneErrorFileTooLarge");
            break;
          default:
            errorMessage = genericError;
            break;
        }

        showAlert(errorMessage, "error");
      } else {
        setSelectedFile(acceptedFiles[0]);
      }
    },
    [acceptsAllFileTypes, t, getAcceptedFileTypesText, showAlert, setSelectedFile],
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
      flex: 1,
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      height: "120px",
      padding: "20px",
      border: `2px dashed`,
      borderColor: disabled ? geopilotTheme.palette.primary.inactive : geopilotTheme.palette.primary.main,
      borderRadius: "4px",
      backgroundColor: geopilotTheme.palette.primary.hover,
      outline: "none",
      transition: "border .24s ease-in-out",
      cursor: disabled ? "default" : "pointer",
    }),
    [disabled],
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
        <FlexRowBox sx={{ gap: "10px" }}>
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
