import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext, useEffect, useState } from "react";
import { useGeopilotAuth } from "../../auth";
import LoginIcon from "@mui/icons-material/Login";
import { FlexBox, FlexRowEndBox } from "../../components/styledComponents.ts";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { FormCheckbox, FormContainer, FormInput, FormSelect } from "../../components/form/form.ts";
import SendIcon from "@mui/icons-material/Send";
import { Delivery, Mandate } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { DeliveryStepEnum, DeliverySubmitData } from "./deliveryInterfaces.tsx";
import { BaseButton, CancelButton } from "../../components/buttons.tsx";
import { useTranslation } from "react-i18next";

export const DeliverySubmit = () => {
  const { t } = useTranslation();
  const { authEnabled, user, login } = useGeopilotAuth();
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useApi();
  const { setStepError, validationResponse, isLoading, submitDelivery, resetDelivery } = useContext(DeliveryContext);
  const [mandates, setMandates] = useState<Mandate[]>([]);
  const [previousDeliveries, setPreviousDeliveries] = useState<Delivery[]>([]);

  useEffect(() => {
    if (validationResponse?.jobId && user) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId: validationResponse.jobId })).then(
        mandates => {
          if (mandates.length === 0) {
            setStepError(DeliveryStepEnum.Submit, t("noMandatesFound"));
          }
          setMandates(mandates);
        },
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [validationResponse, user]);

  const submitForm = (data: FieldValues) => {
    if (data["precursor"] === "") {
      data["precursor"] = null;
    }
    submitDelivery(data as DeliverySubmitData);
  };

  const handleMandateChange = (mandateId: number) => {
    formMethods.setValue("precursor", undefined);
    if (mandateId) {
      fetchApi<Delivery[]>("/api/v1/delivery?" + new URLSearchParams({ mandateId: mandateId.toString() })).then(
        setPreviousDeliveries,
      );
    } else {
      setPreviousDeliveries([]);
    }
  };

  return authEnabled && user ? (
    <FormProvider {...formMethods}>
      <form onSubmit={formMethods.handleSubmit(submitForm)}>
        <FlexBox>
          <FormContainer>
            <FormSelect
              fieldName="mandate"
              label="mandate"
              required={true}
              disabled={mandates.length === 0}
              values={mandates
                ?.sort((a, b) => a.name.localeCompare(b.name))
                .map(mandate => ({ key: mandate.id, name: mandate.name }))}
              onUpdate={handleMandateChange}
            />
            <FormSelect
              fieldName="precursor"
              label="precursor"
              disabled={previousDeliveries.length === 0}
              values={previousDeliveries.map(delivery => ({ key: delivery.id, name: delivery.date.toLocaleString() }))}
            />
          </FormContainer>
          <FormContainer>
            <FormCheckbox fieldName="isPartial" label="isPartialDelivery" checked={false} />
          </FormContainer>
          <FormContainer>
            <FormInput fieldName="comment" label="comment" multiline={true} rows={3} />
          </FormContainer>
          <FlexRowEndBox>
            <CancelButton onClick={() => resetDelivery()} disabled={isLoading} />
            <BaseButton
              icon={<SendIcon />}
              label="createDelivery"
              disabled={!formMethods.formState.isValid || isLoading}
              onClick={() => formMethods.handleSubmit(submitForm)()}
            />
          </FlexRowEndBox>
        </FlexBox>
      </form>
    </FormProvider>
  ) : (
    <FlexRowEndBox>
      <CancelButton onClick={() => resetDelivery()} />
      <BaseButton onClick={login} icon={<LoginIcon />} label="logInForDelivery" />
    </FlexRowEndBox>
  );
};
