import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { FormHelperText, Stack, Typography } from "@mui/material";
import {
  AvailablePipelinesResponse,
  FieldEvaluationType,
  Mandate,
  Organisation,
  PipelineSummary,
} from "../../../api/apiInterfaces.ts";
import { Language } from "../../../appInterfaces.ts";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import {
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormLanguageTabs,
  FormLocalizedInput,
  FormSelect,
} from "../../../components/form/form.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";
import MandateConfigurationFields from "./mandateConfigurationFields.tsx";

const MandateDetail = () => {
  const { t, i18n } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [pipelines, setPipelines] = useState<PipelineSummary[]>();
  const [activeLanguage, setActiveLanguage] = useState<Language>(i18n.resolvedLanguage as Language);

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
        name: {},
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
    // Clear eligible organisations and allowDelivery if mandate is public
    if (formData["isPublic"]) {
      mandate.organisations = [];
      mandate.allowDelivery = false;
    } else {
      mandate.organisations = formData["organisations"]?.map(
        (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
      );
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
        <Stack direction="row" justifyContent="space-between">
          <Typography variant={"h3"} marginTop={0}>
            {t("general")}
          </Typography>
          <FormLanguageTabs language={activeLanguage} onLanguageChange={setActiveLanguage} />
        </Stack>
        <FormContainer>
          <FormLocalizedInput
            fieldName={"name"}
            label={"name"}
            value={mandate?.name}
            activeLanguage={activeLanguage}
            requireAtLeastOne={true}
          />
        </FormContainer>
        <FormContainer>
          <FormLocalizedInput
            fieldName={"description"}
            label={"description"}
            value={mandate?.description}
            activeLanguage={activeLanguage}
            multiline={true}
            minRows={3}
            maxRows={3}
            helperText={t("mandateDescriptionHelperText")}
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
        <Stack direction="row">
          <Typography variant={"h3"} marginTop={0}>
            {t("spatialExtent")}
          </Typography>
          <FormHelperText>{t("spatialExtentHelperText")}</FormHelperText>
        </Stack>
        <FormContainer>
          <FormExtent fieldName={"coordinates"} value={mandate?.coordinates} required={true} />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default MandateDetail;
