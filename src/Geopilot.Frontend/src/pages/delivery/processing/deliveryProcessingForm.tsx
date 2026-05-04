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
import { isProcessingFinished } from "../deliveryUtils";

export const DeliveryProcessingForm = () => {
  const {
    resetDelivery,
    startProcessing,
    jobId,
    processingResponse,
    setStepError,
    setSelectedMandate,
    isLoading,
    isProcessing,
  } = useContext(DeliveryContext);
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [mandates, setMandates] = useState<Mandate[]>([]);

  useEffect(() => {
    if (jobId) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId })).then(mandates => {
        if (mandates.length === 0) {
          setStepError(DeliveryStepEnum.Process, "noMandatesFound");
        }
        setMandates(mandates);
      });
    }
  }, [jobId, fetchApi, setStepError, t, user]);

  const submitForm = (data: FieldValues) => {
    startProcessing({
      mandateId: data["mandate"],
    });
    setSelectedMandate(mandates.find(m => m.id === data["mandate"]));
  };

  const isFormLocked = isLoading || isProcessing || isProcessingFinished(processingResponse);

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

      {!isProcessing && !isProcessingFinished(processingResponse) && (
        <FlexRowEndBox>
          <CancelButton onClick={() => resetDelivery()} />
          <BaseButton
            onClick={() => formMethods.handleSubmit(submitForm)()}
            icon={<PublishedWithChangesIcon />}
            label="process"
            disabled={isLoading}
          />
        </FlexRowEndBox>
      )}
    </FormProvider>
  );
};
