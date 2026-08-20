import { FC, useContext, useEffect, useState } from "react";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Alert, Divider, Stack, Typography } from "@mui/material";
import { DeliverySummary, FieldEvaluationType } from "../../api/apiInterfaces.ts";
import { Button } from "../../components/buttons.tsx";
import { FormCheckbox, FormContainer, FormInput, FormSelect } from "../../components/form/form.ts";
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
  const formMethods = useForm({ mode: "all", defaultValues: submittedData, disabled: completed });

  const submitForm = (data: FieldValues) => {
    if (data["precursor"] === "") {
      data["precursor"] = null;
    }
    submitDelivery(data as DeliverySubmitData);
  };

  // Fetch previous deliveries for the selected mandate
  useEffect(() => {
    if (selectedMandate) {
      fetchApi<DeliverySummary[]>(
        "/api/v1/delivery/summary?" + new URLSearchParams({ mandateId: selectedMandate.id.toString() }),
      ).then(setPreviousDeliveries);
    }
  }, [fetchApi, selectedMandate]);

  const deliveryFiles = processingResponse?.steps.flatMap(step => step.deliveries) ?? [];

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
                  fieldName="precursor"
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
                <FormCheckbox fieldName="isPartial" label="isPartialDelivery" checked={false} disabled={completed} />
              </FormContainer>
            ) : null}
            {selectedMandate && selectedMandate.evaluateComment !== FieldEvaluationType.NotEvaluated ? (
              <FormContainer>
                <FormInput
                  fieldName="comment"
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
        <Alert severity="error">{t("deliveryFilesEmpty")}</Alert>
      ) : (
        <Stack spacing={1}>
          <Typography variant="h5">{t("deliveryFiles")}</Typography>
          <Stack
            spacing={0}
            divider={<Divider aria-hidden="true" />}
            sx={theme => ({
              border: `1px solid ${theme.palette.primary.light}`,
              borderRadius: theme.radius.default,
            })}>
            {deliveryFiles.map(deliveryFile => (
              <Typography key={deliveryFile} color="primary" sx={{ wordBreak: "break-word", p: 1 }}>
                {deliveryFile}
              </Typography>
            ))}
          </Stack>
        </Stack>
      )}
      {completed && <Alert severity="success">{t("deliveryCompleted")}</Alert>}
    </DeliveryContent>
  );
};
