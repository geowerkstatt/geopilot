import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import {
  AvailablePipelinesResponse,
  FieldEvaluationType,
  Mandate,
  Organisation,
  PipelineSummary,
} from "../../../api/apiInterfaces.ts";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import {
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormInput,
  FormSelect,
} from "../../../components/form/form.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";
import MandateConfigurationFields from "./mandateConfigurationFields.tsx";

const MandateDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [pipelines, setPipelines] = useState<PipelineSummary[]>();

  const loadMandate = useCallback(
    async (id: string) => {
      const mandate = await fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" });
      setMandate(mandate);
    },
    [fetchApi],
  );

  const loadOrganisations = useCallback(async () => {
    const organisations = await fetchApi<Organisation[]>("/api/v1/organisation", {
      errorMessageLabel: "organisationsLoadingError",
    });
    setOrganisations(organisations);
  }, [fetchApi]);

  const loadPipelines = useCallback(async () => {
    const pipelines = await fetchApi<AvailablePipelinesResponse>("/api/v1/pipeline", {
      errorMessageLabel: "pipelinesLoadingError",
    });
    setPipelines(pipelines?.pipelines ?? []);
  }, [fetchApi]);

  useEffect(() => {
    if (id !== "0") {
      loadMandate(id);
    } else {
      setMandate({
        id: 0,
        name: "",
        description: {},
        isPublic: false,
        allowDelivery: false,
        organisations: [],
        fileTypes: [],
        coordinates: [
          { x: undefined, y: undefined },
          { x: undefined, y: undefined },
        ],
        deliveries: [],
      });
    }
    loadOrganisations();
    loadPipelines();
  }, [id, loadMandate, loadOrganisations, loadPipelines]);

  const prepareMandateForSave = (formData: FieldValues): Mandate => {
    const mandate = formData as Mandate;
    mandate.deliveries = [];
    // Clear eligible organisations if mandate is public
    mandate.organisations = formData["isPublic"]
      ? []
      : formData["organisations"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as Organisation);

    return mandate;
  };

  return (
    <AdminDetailForm<Mandate>
      basePath="/admin/mandates"
      backLabel="backToMandates"
      data={mandate}
      apiEndpoint="/api/v1/mandate"
      saveErrorLabel="mandateSaveError"
      prepareDataForSave={prepareMandateForSave}
      onSaveSuccess={setMandate}>
      <GeopilotBox>
        <Typography variant={"h3"} marginTop={0}>
          {t("general")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"name"} label={"name"} value={mandate?.name} required={true} />
        </FormContainer>
        <FormContainer>
          <FormInput
            fieldName={"description.de"}
            label={"description"}
            value={mandate?.description?.de}
            multiline={true}
            minRows={3}
          />
        </FormContainer>
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} marginTop={0}>
          {t("configuration")}
        </Typography>
        <MandateConfigurationFields mandate={mandate} organisations={organisations} pipelines={pipelines} />
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} marginTop={0}>
          {t("deliveryForm")}
        </Typography>
        <FormContainer>
          <FormSelect
            fieldName={"evaluatePrecursorDelivery"}
            label={"precursor"}
            required={true}
            selected={mandate?.evaluatePrecursorDelivery}
            values={[
              { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
              { key: 1, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
              { key: 2, value: FieldEvaluationType.Required, name: t("fieldRequired") },
            ]}
          />
          <FormSelect
            fieldName={"evaluatePartial"}
            label={"partialDelivery"}
            required={true}
            selected={mandate?.evaluatePartial}
            values={[
              { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
              { key: 1, value: FieldEvaluationType.Required, name: t("fieldRequired") },
            ]}
          />
        </FormContainer>
        <FormContainerHalfWidth>
          <FormSelect
            fieldName={"evaluateComment"}
            label={"comment"}
            required={true}
            selected={mandate?.evaluateComment}
            values={[
              { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
              { key: 1, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
              { key: 2, value: FieldEvaluationType.Required, name: t("fieldRequired") },
            ]}
          />
        </FormContainerHalfWidth>
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} marginTop={0}>
          {t("spatialExtent")}
        </Typography>
        <FormContainer>
          <FormExtent fieldName={"coordinates"} value={mandate?.coordinates} required={true} />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default MandateDetail;
