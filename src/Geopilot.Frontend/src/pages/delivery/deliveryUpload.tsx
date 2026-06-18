import { FileDropzone } from "../../components/fileDropzone.tsx";
import { FC, useCallback, useContext, useEffect, useState } from "react";
import { ProcessingSettings } from "../../api/apiInterfaces.ts";
import { FormProvider, useForm } from "react-hook-form";
import { FlexBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Trans } from "react-i18next";
import { Link } from "@mui/material";
import { FormCheckbox } from "../../components/form/form.ts";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepEnum, DeliveryStepProps } from "./deliveryInterfaces.tsx";
import { BaseButton, CancelButton } from "../../components/buttons.tsx";
import useFetch from "../../hooks/useFetch.ts";
import { DeliveryContent } from "./deliveryContent.tsx";
import { DeliveryContinueButton } from "./deliveryButtons.tsx";

export const DeliveryUpload: FC<DeliveryStepProps> = ({ completed }) => {
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
    resetDelivery,
  } = useContext(DeliveryContext);

  useEffect(() => {
    if (!processingSettings) {
      fetchApi<ProcessingSettings>("/api/v2/processing").then(setProcessingSettings);
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

  const button = completed ? (
    <>
      <CancelButton
        onClick={() => {
          formMethods.reset();
          resetDelivery();
        }}
      />
      <DeliveryContinueButton />
    </>
  ) : isLoading ? (
    <CancelButton onClick={() => cancelUpload()} />
  ) : (
    <BaseButton
      disabled={!formMethods.formState.isValid || selectedFiles.length === 0}
      onClick={() => formMethods.handleSubmit(submitForm)()}
      label="upload"
    />
  );

  return (
    <DeliveryContent title="upload" subtitle="uploadSubtitle" buttons={button}>
      {initialized && (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <FlexBox>
              <FileDropzone
                selectedFiles={selectedFiles}
                addFiles={addFiles}
                removeFile={removeFile}
                fileUploadStatus={fileUploadStatus}
                fileExtensions={processingSettings?.allowedFileExtensions}
                disabled={completed || isLoading}
                hideDropzone={completed}
                setFileError={setFileError}
                maxFileSizeMB={uploadSettings?.enabled ? uploadSettings.maxFileSizeMB : undefined}
                maxFiles={uploadSettings?.enabled ? uploadSettings.maxFilesPerJob : 1}
                maxTotalFileSizeMB={uploadSettings?.enabled ? uploadSettings.maxJobSizeMB : undefined}
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
                  checked={completed || !termsOfUse}
                  disabled={completed || isLoading}
                  validation={{ required: true }}
                  sx={{ visibility: termsOfUse ? "visible" : "hidden" }}
                />
              </FlexRowSpaceBetweenBox>
            </FlexBox>
          </form>
        </FormProvider>
      )}
    </DeliveryContent>
  );
};
