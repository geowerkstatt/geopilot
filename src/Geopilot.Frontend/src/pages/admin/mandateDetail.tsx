import { useCallback, useEffect, useState } from "react";
import { Typography } from "@mui/material";
import { GeopilotBox } from "../../components/styledComponents.ts";
import {
  FormAutocomplete,
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormInput,
  FormSelect,
} from "../../components/form/form.ts";
import { FieldEvaluationType, Mandate, Organisation, ValidationSettings } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";
import AdminDetailForm from "../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";

export const MandateDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useApi();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();

  const loadMandate = useCallback(() => {
    if (id !== "0") {
      fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" }).then(setMandate);
    } else {
      setMandate({
        id: 0,
        name: "",
        organisations: [],
        fileTypes: [],
        coordinates: [
          { x: undefined, y: undefined },
          { x: undefined, y: undefined },
        ],
        deliveries: [],
      });
    }
  }, [fetchApi, id]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const loadFileExtensions = useCallback(() => {
    fetchApi<ValidationSettings>("/api/v1/validation", { errorMessageLabel: "fileTypesLoadingError" }).then(
      validation => {
        setFileExtensions(validation?.allowedFileExtensions);
      },
    );
  }, [fetchApi]);

  useEffect(() => {
    if (mandate === undefined) {
      loadMandate();
    }
    if (organisations === undefined) {
      loadOrganisations();
    }
    if (fileExtensions === undefined) {
      loadFileExtensions();
    }
  }, [mandate, organisations, fileExtensions, loadMandate, loadOrganisations, loadFileExtensions]);

  const prepareMandateForSave = (formData: FieldValues): Mandate => {
    const mandate = formData as Mandate;
    mandate.deliveries = [];
    mandate.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
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
          <FormAutocomplete
            fieldName={"organisations"}
            label={"eligibleOrganisations"}
            required={false}
            values={organisations}
            selected={mandate?.organisations}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete
            fieldName={"fileTypes"}
            label={"fileTypes"}
            required={false}
            values={fileExtensions}
            selected={mandate?.fileTypes}
          />
        </FormContainer>
        <FormContainer>
          <FormExtent fieldName={"coordinates"} label={"spatialExtent"} value={mandate?.coordinates} required={true} />
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
