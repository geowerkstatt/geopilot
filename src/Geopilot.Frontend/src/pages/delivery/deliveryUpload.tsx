import { FileDropzone } from "../../components/fileDropzone.tsx";
import { useCallback, useContext, useEffect, useState } from "react";
import { ProcessingSettings } from "../../api/apiInterfaces.ts";
import { FormProvider, useForm } from "react-hook-form";
import { FlexBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Trans } from "react-i18next";
import { Link } from "@mui/material";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import { FormCheckbox } from "../../components/form/form.ts";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepEnum } from "./deliveryInterfaces.tsx";
import { BaseButton, CancelButton } from "../../components/buttons.tsx";
import useFetch from "../../hooks/useFetch.ts";

export const DeliveryUpload = () => {
  const [processingSettings, setProcessingSettings] = useState<ProcessingSettings>();
  const { initialized, termsOfUse } = useAppSettings();
  const { fetchApi } = useFetch();
  const formMethods = useForm({ mode: "all" });
  const {
    setStepError,
    selectedFiles,
    addFiles,
    removeFile,
    fileUploadStatus,
    isLoading,
    uploadFile,
    cancelUpload,
    uploadSettings,
  } = useContext(DeliveryContext);

  useEffect(() => {
    if (!processingSettings) {
      fetchApi<ProcessingSettings>("/api/v1/processing").then(setProcessingSettings);
    }
  }, [fetchApi, processingSettings]);

  const submitForm = () => {
    setStepError(DeliveryStepEnum.Upload, undefined);
    uploadFile();
  };

  const setFileError = useCallback(
    (error: string | undefined) => {
      setStepError(DeliveryStepEnum.Upload, error);
    },
    [setStepError],
  );

  return (
    initialized && (
      <FormProvider {...formMethods}>
        <form onSubmit={formMethods.handleSubmit(submitForm)}>
          <FlexBox>
            <FileDropzone
              selectedFiles={selectedFiles}
              addFiles={addFiles}
              removeFile={removeFile}
              fileUploadStatus={fileUploadStatus}
              fileExtensions={processingSettings?.allowedFileExtensions}
              disabled={isLoading}
              setFileError={setFileError}
              maxFileSizeMB={uploadSettings?.enabled ? uploadSettings.maxFileSizeMB : undefined}
              maxFiles={uploadSettings?.enabled ? uploadSettings.maxFilesPerJob : 1}
              isUploading={isLoading}
            />
            <FlexRowSpaceBetweenBox>
              <FormCheckbox
                fieldName="acceptTermsOfUse"
                label={
                  <Trans
                    i18nKey="termsOfUseAcceptance"
                    components={{
                      termsLink: <Link href="/about#termsofuse" target="_blank" />,
                    }}
                  />
                }
                checked={!termsOfUse}
                disabled={isLoading}
                validation={{ required: true }}
                sx={{ visibility: termsOfUse ? "visible" : "hidden" }}
              />
              {isLoading ? (
                <CancelButton onClick={() => cancelUpload()} />
              ) : (
                <BaseButton
                  disabled={!formMethods.formState.isValid || selectedFiles.length === 0}
                  onClick={() => formMethods.handleSubmit(submitForm)()}
                  icon={<CloudUploadOutlinedIcon />}
                  label="upload"
                />
              )}
            </FlexRowSpaceBetweenBox>
          </FlexBox>
        </form>
      </FormProvider>
    )
  );
};
