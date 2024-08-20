import { FC, useCallback, useContext, useEffect, useState } from "react";
import { Box, IconButton, Typography } from "@mui/material";
import { styled } from "@mui/system";
import ClearIcon from "@mui/icons-material/Clear";
import { FileRejection, useDropzone } from "react-dropzone";
import { useTranslation } from "react-i18next";
import { geopilotTheme } from "../appTheme";
import { FlexColumnBox, FlexRowBox } from "./styledComponents.ts";
import { AlertContext } from "./alert/alertContext.tsx";

const DropZoneBox = styled(FlexColumnBox)(({ theme }) => ({
  width: "100%",
  height: "120px",
  border: `2px dashed ${theme.palette.primary.main}`,
  borderRadius: "8px",
  backgroundColor: theme.palette.primary.hover,
  justifyContent: "center",
  alignItems: "center",
  cursor: "pointer",
}));

interface FileDropzoneProps {
  selectedFile?: File;
  setSelectedFile: (file: File | undefined) => void;
  fileExtensions?: string[];
}

export const FileDropzone: FC<FileDropzoneProps> = ({ selectedFile, setSelectedFile, fileExtensions }) => {
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
  });

  const handleRemove = () => {
    setSelectedFile(undefined);
  };

  return (
    <Box {...getRootProps()}>
      <input {...getInputProps()} />
      <DropZoneBox>
        {!selectedFile ? (
          <>
            <FlexRowBox>
              <Typography
                variant="body1"
                sx={{ color: geopilotTheme.palette.primary.main, textDecoration: "underline" }}>
                {t("clickToUpload")}
              </Typography>
              <Typography variant="body1">
                &nbsp;
                {t("or")} {t("dragAndDrop")}
              </Typography>
            </FlexRowBox>

            {fileExtensions && fileExtensions.length > 0 && (
              <Typography variant="caption">{getAcceptedFileTypesText()}&nbsp;(max. 200 MB)</Typography>
            )}
          </>
        ) : (
          <FlexRowBox sx={{ gap: "10px" }}>
            <Typography variant="body1" sx={{ color: geopilotTheme.palette.primary.main }}>
              {selectedFile?.name}
            </Typography>
            <IconButton
              onClick={e => {
                e.stopPropagation();
                handleRemove();
              }}>
              <ClearIcon />
            </IconButton>
          </FlexRowBox>
        )}
      </DropZoneBox>
    </Box>
  );
};
