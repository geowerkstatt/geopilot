import { FC, useContext, useEffect, useState } from "react";
import { FormProvider, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Alert, Stack, Typography } from "@mui/material";
import { DeliverySummary, FieldEvaluationType } from "../../api/generated";
import { Button } from "../../components/buttons.tsx";
import { FormCheckbox, FormContainer, FormInput, FormSelect } from "../../components/form/form.ts";
import { BulletList } from "../../components/styledComponents.ts";
import useFetch from "../../hooks/useFetch.ts";
import { DeliveryBackButton, DeliveryContinueButton } from "./deliveryButtons.tsx";
import { DeliveryContent } from "./deliveryContent.tsx";
import { DeliveryContext } from "./deliveryContext.tsx";
import { DeliveryStepProps, DeliverySubmitData } from "./deliveryInterfaces.tsx";

export const DeliverySubmit: FC<DeliveryStepProps> = ({ completed }) => {
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { isLoading, submitDelivery, selectedMandate, submittedData, processingResponse } = useContext(DeliveryContext);
  const [previousDeliveries, setPreviousDeliveries] = useState<DeliverySummary[]>([]);
  const formMethods = useForm<DeliverySubmitData>({ mode: "all", defaultValues: submittedData, disabled: completed });

  const submitForm = (data: DeliverySubmitData) => {
    // An unselected FormSelect returns an empty string, convert to null for the API
    if ((data.precursorDeliveryId as unknown) === "") {
      data.precursorDeliveryId = null;
    }
    submitDelivery(data);
  };

  // Fetch previous deliveries for the selected mandate
  useEffect(() => {
    if (selectedMandate) {
      fetchApi<DeliverySummary[]>(
        "/api/v1/delivery/summary?" + new URLSearchParams({ mandateId: selectedMandate.id.toString() }),
      ).then(setPreviousDeliveries);
    }
  }, [fetchApi, selectedMandate]);

  const deliveryFiles = processingResponse?.steps.flatMap(step => step.deliveries ?? []) ?? [];

  const buttons = (
    <>
      <DeliveryBackButton />
      {completed ? (
        <DeliveryContinueButton />
      ) : (
        <Button
          variant="contained"
          label="createDelivery"
          disabled={!formMethods.formState.isValid || isLoading || deliveryFiles.length === 0}
          onClick={() => formMethods.handleSubmit(submitForm)()}
        />
      )}
    </>
  );

  return (
    <DeliveryContent title="createDelivery" buttons={buttons}>
      <FormProvider {...formMethods}>
        <form onSubmit={formMethods.handleSubmit(submitForm)}>
          <Stack>
            {selectedMandate && selectedMandate.evaluatePrecursorDelivery !== FieldEvaluationType.NotEvaluated ? (
              <FormContainer>
                <FormSelect
                  fieldName={"precursorDeliveryId" satisfies keyof DeliverySubmitData}
                  label="precursor"
                  required={selectedMandate.evaluatePrecursorDelivery === FieldEvaluationType.Required}
                  disabled={completed || previousDeliveries.length === 0}
                  values={previousDeliveries.map(delivery => ({
                    key: delivery.id,
                    name: new Date(delivery.date).toLocaleString(),
                  }))}
                />
              </FormContainer>
            ) : null}
            {selectedMandate && selectedMandate.evaluatePartial === FieldEvaluationType.Required ? (
              <FormContainer>
                <FormCheckbox
                  fieldName={"partialDelivery" satisfies keyof DeliverySubmitData}
                  label="isPartialDelivery"
                  checked={false}
                  disabled={completed}
                />
              </FormContainer>
            ) : null}
            {selectedMandate && selectedMandate.evaluateComment !== FieldEvaluationType.NotEvaluated ? (
              <FormContainer>
                <FormInput
                  fieldName={"comment" satisfies keyof DeliverySubmitData}
                  label="comment"
                  disabled={completed}
                  required={selectedMandate.evaluateComment === FieldEvaluationType.Required}
                  multiline={true}
                  rows={3}
                />
              </FormContainer>
            ) : null}
            {selectedMandate &&
              selectedMandate.evaluatePrecursorDelivery === FieldEvaluationType.NotEvaluated &&
              selectedMandate.evaluatePartial === FieldEvaluationType.NotEvaluated &&
              selectedMandate.evaluateComment === FieldEvaluationType.NotEvaluated && (
                <Typography variant="body1">{t("deliveryNoInputRequired")}</Typography>
              )}
          </Stack>
        </form>
      </FormProvider>
      {deliveryFiles.length === 0 ? (
        <Alert severity="error" data-cy="delivery-files-empty">
          {t("deliveryFilesEmpty")}
        </Alert>
      ) : (
        <Stack spacing={0} sx={{ mt: 1 }}>
          <Typography variant="body1">{t("deliveryFiles")}</Typography>
          <BulletList>
            {deliveryFiles.map((deliveryFile, index) => (
              <li key={index}>
                <Typography variant="body1" sx={{ wordBreak: "break-word" }}>
                  {deliveryFile}
                </Typography>
              </li>
            ))}
          </BulletList>
        </Stack>
      )}
      {completed && <Alert severity="success">{t("deliveryCompleted")}</Alert>}
    </DeliveryContent>
  );
};
