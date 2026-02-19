import { useCallback, useEffect, useState } from "react";
import { Typography } from "@mui/material";
import { FlexRowSpaceBetweenBox, GeopilotBox } from "../../../components/styledComponents.ts";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormInput,
  FormSelect,
} from "../../../components/form/form.ts";
import {
  AvailablePipelinesResponse,
  FieldEvaluationType,
  Mandate,
  Organisation,
  PipelineSummary,
  ValidatorConfiguration,
} from "../../../api/apiInterfaces.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import useFetch from "../../../hooks/useFetch.ts";
import InterlisProfileFormSelect from "./interlisProfileFormSelect.tsx";
import PipelineFormSelect from "./pipelineFormSelect.tsx";

const MandateDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [pipelines, setPipelines] = useState<PipelineSummary[]>();
  const [validators, setValidators] = useState<{ [key: string]: ValidatorConfiguration }>({});

  const interlisValidatorName = "INTERLIS";

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

  const loadValidators = useCallback(async () => {
    const validators = await fetchApi<{ [key: string]: ValidatorConfiguration }>("/api/v1/validator", {
      errorMessageLabel: "validatorsLoadingError",
    });
    setValidators(validators ?? {});
  }, [fetchApi]);

  useEffect(() => {
    if (id !== "0") {
      loadMandate(id);
    } else {
      setMandate({
        id: 0,
        name: "",
        isPublic: false,
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
    loadValidators();
  }, [id, loadValidators, loadMandate, loadOrganisations, loadPipelines]);

  const prepareMandateForSave = (formData: FieldValues): Mandate => {
    const mandate = formData as Mandate;
    mandate.deliveries = [];
    mandate.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );

    if (mandate.interlisValidationProfile === "") {
      mandate.interlisValidationProfile = undefined;
    }

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
        <Typography variant={"h3"} margin={0}>
          {t("description")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"name"} label={"name"} value={mandate?.name} required={true} />
        </FormContainer>
        <FormContainer>
          <FormCheckbox fieldName={"isPublic"} label={"public"} checked={mandate?.isPublic ?? false} />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<Organisation>
            fieldName={"organisations"}
            label={"eligibleOrganisations"}
            required={false}
            values={organisations}
            selected={mandate?.organisations}
            valueFormatter={org => ({
              id: org.id,
              primaryText: org.name,
              detailText: `${org.name} (ID: ${org.id})`,
            })}
          />
        </FormContainer>
        <FormContainer>
          <PipelineFormSelect pipelines={pipelines} selected={mandate?.pipelineId} />
        </FormContainer>
        <FormContainer>
          <FormExtent fieldName={"coordinates"} label={"spatialExtent"} value={mandate?.coordinates} required={true} />
        </FormContainer>
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} margin={0}>
          {t("validationForm")}
        </Typography>
        <GeopilotBox>
          <FlexRowSpaceBetweenBox>
            <Typography variant={"h4"} margin={0}>
              {interlisValidatorName}
            </Typography>
            <span>
              <span>{t("fileTypes")}: </span>
              <span>{validators[interlisValidatorName]?.supportedFileExtensions.join(", ") ?? ""}</span>
            </span>
          </FlexRowSpaceBetweenBox>
          <FormContainer>
            <InterlisProfileFormSelect
              profiles={validators[interlisValidatorName]?.profiles}
              selected={mandate?.interlisValidationProfile}
            />
          </FormContainer>
        </GeopilotBox>
        <FormContainer>
          <FormAutocomplete<string>
            fieldName={"fileTypes"}
            label={"fileTypes"}
            required={false}
            values={validators[interlisValidatorName]?.supportedFileExtensions ?? []}
            selected={mandate?.fileTypes}
          />
        </FormContainer>
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} margin={0}>
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
    </AdminDetailForm>
  );
};

export default MandateDetail;
