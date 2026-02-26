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
import { useGeopilotAuth } from "../../../auth";
import { isValidationFinished } from "../deliveryUtils";

export const DeliveryValidationForm = () => {
  const { resetDelivery, validateFile, jobId, validationResponse, setStepError, setSelectedMandate, isLoading, isValidating } =
    useContext(DeliveryContext);
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [mandates, setMandates] = useState<Mandate[]>([]);

  // Fetch mandates for the current job
  useEffect(() => {
    if (jobId) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId })).then(mandates => {
        if (mandates.length === 0) {
          setStepError(DeliveryStepEnum.Validate, "noMandatesFound");
        }
        setMandates(mandates);
      });
    }
  }, [jobId, fetchApi, setStepError, t, user]);

  const submitForm = (data: FieldValues) => {
    validateFile({
      mandateId: data["mandate"],
    });
    setSelectedMandate(mandates.find(m => m.id === data["mandate"]));
  };

  const isFormLocked = isLoading || isValidating || isValidationFinished(validationResponse);

  return (
    <FormProvider {...formMethods}>
      <FormSelect
        fieldName="mandate"
        label="mandate"
        required={true}
        disabled={isFormLocked}
        values={mandates
          ?.sort((a, b) => a.name.localeCompare(b.name))
          .map(mandate => ({ key: mandate.id, name: mandate.name }))}
      />

      {!isValidating && !isValidationFinished(validationResponse) && (
        <FlexRowEndBox>
          <CancelButton onClick={() => resetDelivery()} />
          <BaseButton
            onClick={() => formMethods.handleSubmit(submitForm)()}
            icon={<PublishedWithChangesIcon />}
            label="validate"
            disabled={isLoading}
          />
        </FlexRowEndBox>
      )}
    </FormProvider>
  );
};
