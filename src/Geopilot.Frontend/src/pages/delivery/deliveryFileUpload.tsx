import { FC, useCallback, useContext, useEffect, useState } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { Trans, useTranslation } from "react-i18next";
import { Alert, Link, Stack } from "@mui/material";
import { DeliveryStepState } from "../../api/apiInterfaces.ts";
import { MandateSummary, ProcessingSettingsResponse } from "../../api/generated";
import { useGeopilotAuth } from "../../auth";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { Button } from "../../components/buttons.tsx";
import { FileDropzone } from "../../components/fileDropzone.tsx";
import { FormCheckbox } from "../../components/form/form.ts";
import useFetch from "../../hooks/useFetch.ts";
import { DeliveryContent } from "./deliveryContent.tsx";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepEnum, DeliveryStepProps } from "./deliveryInterfaces.tsx";

export const DeliveryFileUpload: FC<DeliveryStepProps> = ({ completed }) => {
  const [processingSettings, setProcessingSettings] = useState<ProcessingSettingsResponse>();
  const { initialized, termsOfUse } = useAppSettings();
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const formMethods = useForm({ mode: "all" });
  const {
    setStepStatus,
    selectedFiles,
    addFiles,
    removeFile,
    fileUploadStatus,
    isLoading,
    uploadFile,
    uploadSettings,
    lastCompletedStep,
  } = useContext(DeliveryContext);

  useEffect(() => {
    if (!processingSettings) {
      fetchApi<ProcessingSettingsResponse>("/api/v2/processing").then(setProcessingSettings);
    }
  }, [fetchApi, processingSettings]);

  useEffect(() => {
    // Reset the form state when the user restarts the delivery process
    formMethods.reset();
  }, [formMethods, lastCompletedStep]);

  const { user, authLoaded, login } = useGeopilotAuth();
  // Undefined while the sign-in is still resolving, null for an anonymous visitor.
  const userId = user === undefined ? undefined : (user?.id ?? null);
  const [mandateCheck, setMandateCheck] = useState<{ userId: number | null; isEmpty: boolean }>();

  useEffect(() => {
    // The API identifies the caller by the geopilot.auth cookie, so only ask once it is settled who is asking.
    if (userId === undefined) return;
    let isCurrent = true;
    fetchApi<MandateSummary[]>("/api/v1/mandate/summary")
      .then(mandates => mandates.length === 0)
      .catch(() => false)
      .then(isEmpty => {
        if (isCurrent) setMandateCheck({ userId, isEmpty });
      });
    return () => {
      isCurrent = false;
    };
  }, [fetchApi, userId]);

  // An answer given for someone else, for example before a sign-in, locks nothing.
  const hasNoMandate = mandateCheck?.isEmpty === true && mandateCheck.userId === userId;

  const submitForm = () => {
    setStepStatus(DeliveryStepEnum.Files, undefined);
    uploadFile();
  };

  const setFileError = useCallback(
    (error: string | undefined) => {
      setStepStatus(DeliveryStepEnum.Files, error ? DeliveryStepState.Error : undefined, error ? [error] : undefined);
    },
    [setStepStatus],
  );

  const button = completed ? undefined : (
    <Button
      variant="contained"
      disabled={isLoading || hasNoMandate || !formMethods.formState.isValid || selectedFiles.length === 0}
      onClick={() => formMethods.handleSubmit(submitForm)()}
      label="upload"
    />
  );

  return (
    <DeliveryContent title="files" subtitle="uploadSubtitle" buttons={button}>
      {initialized && (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <Stack>
              <FileDropzone
                selectedFiles={selectedFiles}
                addFiles={addFiles}
                removeFile={removeFile}
                fileUploadStatus={fileUploadStatus}
                fileExtensions={processingSettings?.allowedFileExtensions}
                disabled={completed || isLoading || hasNoMandate}
                hideDropzone={completed}
                setFileError={setFileError}
                maxFileSizeMB={uploadSettings?.maxFileSizeMB}
                maxFiles={uploadSettings?.maxFilesPerJob}
                maxTotalFileSizeMB={uploadSettings?.maxJobSizeMB}
                isUploading={isLoading}
              />
              {!completed && hasNoMandate && (
                <Alert severity="info" data-cy="no-mandate-available">
                  {user ? (
                    t("noMandateAvailableSignedIn")
                  ) : (
                    <Trans
                      i18nKey="noMandateAvailableAnonymous"
                      components={{
                        loginLink: authLoaded ? (
                          <Link component="button" type="button" onClick={login} data-cy="no-mandate-login-link" />
                        ) : (
                          <span />
                        ),
                      }}
                    />
                  )}
                </Alert>
              )}
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
            </Stack>
          </form>
        </FormProvider>
      )}
    </DeliveryContent>
  );
};
