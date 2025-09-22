import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { FormSelect } from "../../../components/form/formSelect";
import { FlexRowEndBox } from "../../../components/styledComponents";
import { BaseButton, CancelButton } from "../../../components/buttons";
import { useContext, useEffect, useState } from "react";
import { DeliveryContext } from "../deliveryContext";
import useFetch from "../../../hooks/useFetch";
import { useTranslation } from "react-i18next";
import { Mandate } from "../../../api/apiInterfaces";
import { DeliveryStepEnum } from "../deliveryInterfaces";
import PublishedWithChangesIcon from "@mui/icons-material/PublishedWithChanges";

export const DeliveryValidationForm = () => {
  const { resetDelivery, validateFile, validationResponse, setStepError, setSelectedMandate } =
    useContext(DeliveryContext);
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const [mandates, setMandates] = useState<Mandate[]>([]);

  // Fetch mandates for the current job
  useEffect(() => {
    if (validationResponse?.jobId) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId: validationResponse.jobId })).then(
        mandates => {
          if (mandates.length === 0) {
            setStepError(DeliveryStepEnum.Submit, t("noMandatesFound"));
          }
          setMandates(mandates);
        },
      );
    }
  }, [validationResponse, fetchApi, setStepError, t]);

  const submitForm = (data: FieldValues) => {
    validateFile(validationResponse!.jobId, {
      mandateId: data["mandate"],
    });
    setSelectedMandate(mandates.find(m => m.id === data["mandate"]));
  };

  const isFormActive = !formMethods.formState.isSubmitting && !formMethods.formState.isSubmitSuccessful;

  return (
    <FormProvider {...formMethods}>
      <FormSelect
        fieldName="mandate"
        label="mandate"
        required={true}
        disabled={!isFormActive}
        values={mandates
          ?.sort((a, b) => a.name.localeCompare(b.name))
          .map(mandate => ({ key: mandate.id, name: mandate.name }))}
      />

      {isFormActive && (
        <FlexRowEndBox>
          <CancelButton onClick={() => resetDelivery()} />
          <BaseButton
            onClick={() => formMethods.handleSubmit(submitForm)()}
            icon={<PublishedWithChangesIcon />}
            label="validate"
          />
        </FlexRowEndBox>
      )}
    </FormProvider>
  );
};
