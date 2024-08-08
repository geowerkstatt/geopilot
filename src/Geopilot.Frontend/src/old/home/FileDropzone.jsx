import { useCallback, useState, useEffect } from "react";
import { useDropzone } from "react-dropzone";
import { MdCancel, MdFileUpload } from "react-icons/md";
import { Button, Spinner } from "react-bootstrap";
import styled from "styled-components";
import { useTranslation, Trans } from "react-i18next";
import i18n from "../../i18n.js";

const getColor = isDragActive => {
  if (isDragActive) {
    return "#2196f3";
  } else {
    return "#d1d6d991";
  }
};

const Container = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  min-height: 15vh;
  max-width: 95vw;
  font-size: 20px;
  border-width: 2px;
  border-radius: 5px;
  border-color: ${props => getColor(props.$isDragActive)};
  border-style: dashed;
  background-color: #d1d6d991;
  color: #9f9f9f;
  outline: none;
  transition: border 0.24s ease-in-out;
`;

export const FileDropzone = ({
  setFileToCheck,
  setUploadLogsEnabled,
  fileToCheck,
  nutzungsbestimmungenAvailable,
  checkedNutzungsbestimmungen,
  checkFile,
  validationRunning,
  setCheckedNutzungsbestimmungen,
  showNutzungsbestimmungen,
  acceptedFileTypes,
  fileToCheckRef,
}) => {
  const { t } = useTranslation();
  const [fileAvailable, setFileAvailable] = useState(false);
  const [dropZoneDefaultText, setDropZoneDefaultText] = useState();
  const [dropZoneText, setDropZoneText] = useState(dropZoneDefaultText);
  const [dropZoneTextClass, setDropZoneTextClass] = useState("dropzone dropzone-text-disabled");

  const acceptsAllFileTypes = acceptedFileTypes?.includes(".*") ?? false;
  const acceptedFileTypesText = acceptedFileTypes?.join(", ") ?? "";

  useEffect(() => {
    let fileDescription = acceptsAllFileTypes ? t("file") : `${t("file")} (${acceptedFileTypesText})`;
    if (i18n.language !== "de") {
      fileDescription = fileDescription.toLowerCase();
    }
    setDropZoneDefaultText(t("dropZoneDefaultText", { fileDescription }));
  }, [acceptsAllFileTypes, acceptedFileTypesText, t]);
  useEffect(() => setDropZoneText(dropZoneDefaultText), [dropZoneDefaultText]);

  const onDropAccepted = useCallback(
    acceptedFiles => {
      const updateDropZoneClass = () => {
        if (!checkFile || (nutzungsbestimmungenAvailable && !checkedNutzungsbestimmungen)) {
          setDropZoneTextClass("dropzone dropzone-text-disabled");
        } else {
          setDropZoneTextClass("dropzone dropzone-text-file");
        }
      };
      updateDropZoneClass();
      if (acceptedFiles.length === 1) {
        setDropZoneText(acceptedFiles[0].name);
        updateDropZoneClass();
        setFileToCheck(acceptedFiles[0]);
        fileToCheckRef.current = acceptedFiles[0];
        setFileAvailable(true);
      }
    },
    [checkFile, checkedNutzungsbestimmungen, fileToCheckRef, nutzungsbestimmungenAvailable, setFileToCheck],
  );

  const resetFileToCheck = useCallback(() => {
    setFileToCheck(null);
    fileToCheckRef.current = null;
  }, [fileToCheckRef, setFileToCheck]);

  const onDropRejected = useCallback(
    fileRejections => {
      setDropZoneTextClass("dropzone dropzone-text-error");
      const errorCode = fileRejections[0].errors[0].code;
      const genericError = acceptsAllFileTypes
        ? t("dropZoneErrorChooseFile")
        : t("dropZoneErrorChooseFileOfType", { acceptedFileTypesText: acceptedFileTypesText });

      switch (errorCode) {
        case "file-invalid-type":
          setDropZoneText(t("dropZoneErrorChooseFileOfType", { genericError: genericError }));
          break;
        case "too-many-files":
          setDropZoneText(t("dropZoneErrorTooManyFiles"));
          break;
        case "file-too-large":
          setDropZoneText(t("dropZoneErrorFileTooLarge"));
          break;
        default:
          setDropZoneText(genericError);
          break;
      }
      resetFileToCheck();
      setFileAvailable(false);
    },
    [resetFileToCheck, acceptsAllFileTypes, acceptedFileTypesText, t],
  );

  const removeFile = e => {
    e.stopPropagation();
    setUploadLogsEnabled(false);
    resetFileToCheck();
    setFileAvailable(false);
    setDropZoneText(dropZoneDefaultText);
    setDropZoneTextClass("dropzone dropzone-text-disabled");
  };

  const accept = acceptsAllFileTypes
    ? undefined
    : {
        "application/x-geopilot-files": acceptedFileTypes ?? [],
      };
  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDropAccepted,
    onDropRejected,
    maxFiles: 1,
    maxSize: 209715200,
    accept,
  });

  return (
    <div className="dropzone-wrapper">
      <Container className={dropZoneTextClass} {...getRootProps()} $isDragActive={isDragActive ?? false}>
        <input {...getInputProps()} />
        <div className={dropZoneTextClass}>
          {fileAvailable && (
            <span onClick={removeFile}>
              <MdCancel className="dropzone-icon" />
            </span>
          )}
          {dropZoneText}
          {!fileAvailable && (
            <p className="drop-icon">
              <MdFileUpload />
            </p>
          )}
          {fileToCheck && nutzungsbestimmungenAvailable && (
            <div onClick={e => e.stopPropagation()} className="terms-of-use">
              <label>
                <input
                  type="checkbox"
                  defaultChecked={checkedNutzungsbestimmungen}
                  onChange={() => setCheckedNutzungsbestimmungen(!checkedNutzungsbestimmungen)}
                />
                <span className="nutzungsbestimmungen-input">
                  <Trans
                    i18nKey="termsOfUseAcceptance"
                    components={{
                      button: (
                        <Button
                          variant="link"
                          className="terms-of-use link"
                          onClick={() => {
                            showNutzungsbestimmungen();
                          }}
                        />
                      ),
                    }}
                  />
                </span>
              </label>
            </div>
          )}
          {validationRunning && (
            <div>
              <Spinner className="spinner" animation="border" />
            </div>
          )}
          {fileAvailable && (
            <p className={!nutzungsbestimmungenAvailable && "added-margin"}>
              <Button
                className={fileToCheck && !validationRunning ? "check-button" : "invisible-check-button"}
                onClick={checkFile}
                disabled={(nutzungsbestimmungenAvailable && !checkedNutzungsbestimmungen) || validationRunning}>
                {t("validate")}
              </Button>
            </p>
          )}
        </div>
      </Container>
    </div>
  );
};
