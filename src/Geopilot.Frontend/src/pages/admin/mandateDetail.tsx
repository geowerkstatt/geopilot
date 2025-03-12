import { useEffect, useState } from "react";
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

const MandateDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useApi();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();

  const loadMandate = async (id: string) => {
    const mandate = await fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" });
    setMandate(mandate);
  };

  const loadOrganisations = async () => {
    const organisations = await fetchApi<Organisation[]>("/api/v1/organisation", {
      errorMessageLabel: "organisationsLoadingError",
    });
    setOrganisations(organisations);
  };

  const loadFileExtensions = async () => {
    const validation = await fetchApi<ValidationSettings>("/api/v1/validation", {
      errorMessageLabel: "fileTypesLoadingError",
    });
    setFileExtensions(validation?.allowedFileExtensions);
  };

  useEffect(() => {
    if (mandate === undefined) {
      if (id !== "0") {
        loadMandate(id);
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
    }
    if (organisations === undefined) {
      loadOrganisations();
    }
    if (fileExtensions === undefined) {
      loadFileExtensions();
    }
    // We only want to run this once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
          <FormAutocomplete<string>
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
