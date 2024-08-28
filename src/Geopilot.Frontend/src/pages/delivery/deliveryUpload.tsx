import { FileDropzone } from "../../components/fileDropzone.tsx";
import { useContext, useEffect, useState } from "react";
import { ValidationSettings } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { FormProvider, useForm } from "react-hook-form";
import { FlexColumnBox, FlexRowSpaceBetweenBox } from "../../components/styledComponents.ts";
import { Trans, useTranslation } from "react-i18next";
import { Button, Link } from "@mui/material";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { FormCheckbox } from "../../components/form/form.ts";
import { useAppSettings } from "../../components/appSettings/appSettingsInterface.ts";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepEnum } from "./deliveryInterfaces.tsx";

export const DeliveryUpload = () => {
  const { t } = useTranslation();
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
    uploadFile();
  };

  return (
    initialized && (
      <FormProvider {...formMethods}>
        <form onSubmit={formMethods.handleSubmit(submitForm)}>
          <FlexColumnBox>
            <FileDropzone
              selectedFile={selectedFile}
              setSelectedFile={setSelectedFile}
              fileExtensions={validationSettings?.allowedFileExtensions.filter(value => !value.includes("*"))}
              disabled={isLoading}
              setFileError={error => {
                setStepError(DeliveryStepEnum.Upload, error);
              }}
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
                <Button variant="outlined" startIcon={<CancelOutlinedIcon />} onClick={() => resetDelivery()}>
                  {t("cancel")}
                </Button>
              ) : (
                <Button
                  variant="contained"
                  startIcon={<CloudUploadOutlinedIcon />}
                  disabled={!formMethods.formState.isValid || !selectedFile}
                  onClick={() => formMethods.handleSubmit(submitForm)()}>
                  {t("upload")}
                </Button>
              )}
            </FlexRowSpaceBetweenBox>
          </FlexColumnBox>
        </form>
      </FormProvider>
    )
  );
};
