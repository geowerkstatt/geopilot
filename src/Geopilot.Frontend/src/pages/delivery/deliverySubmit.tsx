import { Button } from "@mui/material";

import { DeliveryContext } from "./deliveryContext.tsx";
import { useTranslation } from "react-i18next";
import { useContext, useEffect, useState } from "react";
import { useGeopilotAuth } from "../../auth";
import LoginIcon from "@mui/icons-material/Login";
import {
  FlexColumnBox,
  FlexRowCenterBox,
  FlexRowEndBox,
  FlexRowSpaceBetweenBox,
} from "../../components/styledComponents.ts";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { FormCheckbox, FormInput, FormSelect } from "../../components/form/form.ts";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import SendIcon from "@mui/icons-material/Send";
import { Delivery, Mandate } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { DeliverySubmitData } from "./deliveryInterfaces.tsx";

export const DeliverySubmit = () => {
  const { t } = useTranslation();
  const { enabled, user, login } = useGeopilotAuth();
  const formMethods = useForm({ mode: "all" });
  const { fetchApi } = useApi();
  const { validationResponse, isLoading, submitDelivery, resetDelivery } = useContext(DeliveryContext);
  const [mandates, setMandates] = useState<Mandate[]>([]);
  const [previousDeliveries, setPreviousDeliveries] = useState<Delivery[]>([]);

  useEffect(() => {
    if (validationResponse?.jobId && user) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId: validationResponse.jobId })).then(
        setMandates,
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [validationResponse, user]);

  useEffect(() => {
    const mandateId = formMethods.getValues()["mandate"];
    if (mandateId) {
      fetchApi<Delivery[]>("/api/v1/delivery?" + new URLSearchParams({ mandateId: mandateId })).then(
        setPreviousDeliveries,
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formMethods.getValues()["mandate"]]);

  const submitForm = (data: FieldValues) => {
    submitDelivery(data as DeliverySubmitData);
  };

  return enabled && user ? (
    <FormProvider {...formMethods}>
      <form onSubmit={formMethods.handleSubmit(submitForm)}>
        <FlexColumnBox>
          <FlexRowSpaceBetweenBox>
            <FormSelect
              fieldName="mandate"
              label="mandate"
              required={true}
              values={mandates
                ?.sort((a, b) => a.name.localeCompare(b.name))
                .map(mandate => ({ key: mandate.id, name: mandate.name }))}
            />
            <FormSelect
              fieldName="predecessor"
              label="predecessor"
              values={previousDeliveries.map(delivery => ({ key: delivery.id, name: delivery.date.toLocaleString() }))}
            />
          </FlexRowSpaceBetweenBox>
          <FlexRowSpaceBetweenBox>
            <FormCheckbox fieldName="isPartial" label="isPartialDelivery" checked={false} />
          </FlexRowSpaceBetweenBox>
          <FlexRowSpaceBetweenBox>
            <FormInput fieldName="comment" label="comment" multiline={true} rows={3} />
          </FlexRowSpaceBetweenBox>
          <FlexRowEndBox>
            <Button variant="outlined" startIcon={<CancelOutlinedIcon />} onClick={() => resetDelivery()}>
              {t("cancel")}
            </Button>
            {!isLoading && (
              <Button
                variant="contained"
                startIcon={<SendIcon />}
                disabled={!formMethods.formState.isValid}
                onClick={() => formMethods.handleSubmit(submitForm)()}>
                {t("createDelivery")}
              </Button>
            )}
          </FlexRowEndBox>
        </FlexColumnBox>
      </form>
    </FormProvider>
  ) : (
    <FlexRowCenterBox>
      <Button onClick={login} startIcon={<LoginIcon />} variant="contained" data-cy="loginfordelivery-button">
        {t("logInForDelivery")}
      </Button>
    </FlexRowCenterBox>
  );
};
