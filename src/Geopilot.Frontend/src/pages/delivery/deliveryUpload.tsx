import { FileDropzone } from "../../components/fileDropzone.tsx";
import { useCallback, useContext, useEffect, useState } from "react";
import { ValidationSettings } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
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

export const DeliveryUpload = () => {
  const [validationSettings, setValidationSettings] = useState<ValidationSettings>();
  const { initialized, termsOfUse } = useAppSettings();
  const { fetchApi } = useApi();
  const formMethods = useForm({ mode: "all" });
  const { setStepError, selectedFile, setSelectedFile, isLoading, uploadFile, resetDelivery } =
    useContext(DeliveryContext);

  useEffect(() => {
    if (!validationSettings) {
      fetchApi<ValidationSettings>("/api/v1/validation").then(setValidationSettings);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [validationSettings]);

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
              selectedFile={selectedFile}
              setSelectedFile={setSelectedFile}
              fileExtensions={validationSettings?.allowedFileExtensions}
              disabled={isLoading}
              setFileError={setFileError}
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
                <CancelButton onClick={() => resetDelivery()} />
              ) : (
                <BaseButton
                  disabled={!formMethods.formState.isValid || !selectedFile}
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
