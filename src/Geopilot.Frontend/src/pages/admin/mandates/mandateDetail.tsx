import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Stack, Typography } from "@mui/material";
import { LocalizedText, MandateFormValues } from "../../../api/apiInterfaces.ts";
import {
  AvailablePipelinesResponse,
  Coordinate,
  FieldEvaluationType,
  Mandate,
  MetadataField,
  MetadataFieldType,
  Organisation,
  PipelineSummary,
  ReactionSetSummary,
  StacAccess,
} from "../../../api/generated";
import { isLanguage, Language } from "../../../appInterfaces.ts";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
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
import MandateMetadataFields from "./mandateMetadataFields.tsx";
import ReactionSetFields from "./reactionSetFields.tsx";

const withEveryLanguage = (text: LocalizedText): LocalizedText => ({
  ...Object.fromEntries(Object.values(Language).map(language => [language, ""])),
  ...text,
});

// The form registers every language of a title and an option list for every field. A reset remounts the metadata
// fields, and loaded values that differ from that only by a missing entry would mark the form as changed again.
const inFormShape = (fields: MetadataField[]): MetadataField[] =>
  fields.map(field => ({
    ...field,
    title: withEveryLanguage(field.title),
    options: (field.options ?? []).map(option => ({ ...option, title: withEveryLanguage(option.title) })),
  }));

const MandateDetail = () => {
  const { t, i18n } = useTranslation();
  const { fetchApi } = useFetch();
  const { reactionSetsEnabled } = useCapabilities();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<MandateFormValues>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [pipelines, setPipelines] = useState<PipelineSummary[]>();
  const [reactionSets, setReactionSets] = useState<ReactionSetSummary[]>();
  const [activeLanguage, setActiveLanguage] = useState<Language>(
    isLanguage(i18n.resolvedLanguage) ? i18n.resolvedLanguage : Language.EN,
  );

  const loadMandate = useCallback(
    async (id: string) => {
      const mandate = await fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" });
      setMandate({ ...mandate, metadataFields: inFormShape(mandate.metadataFields) });
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
    setReactionSets(pipelines?.reactionSets ?? []);
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
        stacAccess: StacAccess.AdminsOnly,
        organisations: [],
        fileTypes: [],
        metadataFields: [],
        // Prefill coordinates to allow editing them in the form
        coordinates: [
          { x: undefined, y: undefined },
          { x: undefined, y: undefined },
        ] as Partial<Coordinate>[] as Coordinate[],
        deliveries: [],
      });
    }
    loadOrganisations();
    loadPipelines();
  }, [id, loadMandate, loadOrganisations, loadPipelines]);

  const prepareMandateForSave = (formData: FieldValues): Mandate => {
    const mandate = formData as Mandate;
    mandate.deliveries = [];
    mandate.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );

    // Without a set, the empty values make the server drop the stored ones; values left out would be kept.
    if (!formData["reactionSetId"]) {
      mandate.reactionSetId = null;
      mandate.reactionSetValues = {};
    }

    // Only a selection has options, so every other field leaves them out, like the server does. A field switched away
    // from a selection still holds them in the form.
    mandate.metadataFields = mandate.metadataFields.map(field =>
      field.type === MetadataFieldType.Selection ? field : { ...field, options: undefined },
    );

    return mandate;
  };

  return (
    <AdminDetailForm<MandateFormValues>
      basePath="/admin/mandates"
      backLabel="backToMandates"
      // Waits for the sets as well: a choice of a set that mounted late would be missing from the payload and clear
      // the set of the mandate.
      data={reactionSets ? mandate : undefined}
      apiEndpoint="/api/v1/mandate"
      saveErrorLabel="mandateSaveError"
      prepareDataForSave={prepareMandateForSave}>
      <GeopilotBox>
        <Stack direction="row" sx={{ justifyContent: "space-between" }}>
          <Typography variant={"h3"} sx={{ marginTop: 0 }}>
            {t("general")}
          </Typography>
          <FormLanguageTabs
            language={activeLanguage}
            onLanguageChange={setActiveLanguage}
            label={"generalInputLanguage"}
          />
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
        <Typography variant={"h3"} sx={{ marginTop: 0 }}>
          {t("configuration")}
        </Typography>
        <MandateConfigurationFields mandate={mandate} organisations={organisations} pipelines={pipelines} />
      </GeopilotBox>
      {/* Hiding loses nothing: where no set can be chosen, no mandate holds one, or geopilot would not start. */}
      {reactionSetsEnabled && reactionSets && reactionSets.length > 0 && (
        <GeopilotBox>
          <Typography variant={"h3"} sx={{ marginTop: 0 }}>
            {t("reactions")}
          </Typography>
          <ReactionSetFields mandate={mandate} reactionSets={reactionSets} />
        </GeopilotBox>
      )}
      <GeopilotBox>
        <Typography variant={"h3"} sx={{ marginTop: 0 }}>
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
        <MandateMetadataFields mandate={mandate} />
      </GeopilotBox>
      <GeopilotBox>
        <Typography variant={"h3"} sx={{ marginTop: 0 }}>
          {t("stacBrowser")}
        </Typography>
        <FormContainer>
          <FormSelect
            fieldName={"stacAccess"}
            label={"stacAccess"}
            required={true}
            selected={mandate?.stacAccess}
            values={[
              { key: 0, value: StacAccess.AdminsOnly, name: t("stacAccessAdminsOnly") },
              { key: 1, value: StacAccess.Public, name: t("stacAccessPublic") },
            ]}
          />
        </FormContainer>
        <Typography variant={"body1"}>{t("spatialExtent")}</Typography>
        <FormContainer>
          <FormExtent fieldName={"coordinates"} value={mandate?.coordinates} required={true} />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default MandateDetail;
