import { DeliveryContext } from "./deliveryContext.tsx";
import { useContext, useEffect, useState } from "react";
import { FlexBox, FlexRowEndBox } from "../../components/styledComponents.ts";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { FormCheckbox, FormContainer, FormInput, FormSelect } from "../../components/form/form.ts";
import SendIcon from "@mui/icons-material/Send";
import { Delivery, FieldEvaluationType } from "../../api/apiInterfaces.ts";
import { DeliverySubmitData } from "./deliveryInterfaces.tsx";
import { BaseButton, CancelButton } from "../../components/buttons.tsx";
import useFetch from "../../hooks/useFetch.ts";
import { DifferenceVisualisation } from "./differenceVisualisation.tsx";

export const DeliverySubmit = () => {
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useFetch();
  const { isLoading, submitDelivery, resetDelivery, selectedMandate, selectedFile } = useContext(DeliveryContext);
  const [previousDeliveries, setPreviousDeliveries] = useState<Delivery[]>([]);
  const [requiresApproval, setRequiresApproval] = useState(false);

  const submitForm = (data: FieldValues) => {
    if (data["precursor"] === "") {
      data["precursor"] = null;
    }
    submitDelivery(data as DeliverySubmitData);
  };

  const handlePrecursorChange = (precursorId: number) => {
    setRequiresApproval(precursorId != undefined);
  };

  // Fetch previous deliveries for the selected mandate
  useEffect(() => {
    if (selectedMandate) {
      fetchApi<Delivery[]>(
        "/api/v1/delivery?" + new URLSearchParams({ mandateId: selectedMandate.id.toString() }),
      ).then(setPreviousDeliveries);
    }
  }, [fetchApi, selectedMandate]);

  return (
    <FormProvider {...formMethods}>
      <form onSubmit={formMethods.handleSubmit(submitForm)}>
        <FlexBox>
          <FormContainer>
            {selectedMandate && selectedMandate.evaluatePrecursorDelivery !== FieldEvaluationType.NotEvaluated ? (
              <FormSelect
                fieldName="precursor"
                label="precursor"
                onUpdate={handlePrecursorChange}
                required={selectedMandate.evaluatePrecursorDelivery === FieldEvaluationType.Required}
                disabled={previousDeliveries.length === 0}
                values={previousDeliveries.map(delivery => ({
                  key: delivery.id,
                  name: delivery.date.toLocaleString(),
                }))}
              />
            ) : null}
          </FormContainer>
          {selectedMandate && selectedMandate.evaluatePartial === FieldEvaluationType.Required ? (
            <FormContainer>
              <FormCheckbox fieldName="isPartial" label="isPartialDelivery" checked={false} />
            </FormContainer>
          ) : null}
          {selectedMandate && selectedMandate.evaluateComment !== FieldEvaluationType.NotEvaluated ? (
            <FormContainer>
              <FormInput
                fieldName="comment"
                label="comment"
                required={selectedMandate.evaluateComment === FieldEvaluationType.Required}
                multiline={true}
                rows={3}
              />
            </FormContainer>
          ) : null}
          {requiresApproval && selectedFile?.name === "sh_sha_SH_Nutzungsplanung_V5_0_Zeitstand2.xtf" ? (
            <>
              <FormContainer>
                <DifferenceVisualisation sourceWFS="/mapservice/validationWithID0000" />
              </FormContainer>
              <FormContainer>
                <FormCheckbox
                  fieldName="isApproved"
                  label="isApproved"
                  checked={false}
                  validation={{ validate: (value: boolean) => value }}
                />
              </FormContainer>
            </>
          ) : null}
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
  );
};
