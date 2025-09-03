import { useCallback, useEffect, useState } from "react";
import { Checkbox, FormControlLabel, Typography } from "@mui/material";
import { FlexRowSpaceBetweenBox, GeopilotBox } from "../../components/styledComponents.ts";
import {
  FormAutocomplete,
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormInput,
  FormSelect,
} from "../../components/form/form.ts";
import { FieldEvaluationType, Mandate, Organisation, Profile, ValidationSettings, ValidatorConfiguration } from "../../api/apiInterfaces.ts";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";
import AdminDetailForm from "../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import useFetch from "../../hooks/useFetch.ts";
import i18n from "../../i18n.js";
import { FormSelectValue } from "../../components/form/formSelect.tsx";

const MandateDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [validators, setValidators] = useState<{[key: string]: ValidatorConfiguration}>({});

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

  const loadValidators = useCallback(async () => {
    const validators = await fetchApi<{[key: string]: ValidatorConfiguration}>("/api/v1/validator", {
      errorMessageLabel: "validatorsLoadingError",
    });
    setValidators(validators ?? {});
  }, [fetchApi]);

  // Helper to get the FormSelect menu items for the INTERLIS validation profiles
  const getInterlisProfileSelectMenuItems = (): FormSelectValue[] => {
    return validators[interlisValidatorName]?.profiles.map((profile, idx) => ({
      key: idx,
      value: profile.id,
      name: `${getLocalisedProfileTitle(profile, i18n.language)} (${t("id")}: ${profile.id})`,
    })) ?? [];
  }

  // Helper function to get the localized title for an INTERLIS validation profile
  const getLocalisedProfileTitle = (profile: Profile, language: string): string => {
    if (!profile.titles || profile.titles.length === 0) {
      return profile.id;
    }

    // Look for title in the current language first
    const germanTitle = profile.titles.find((title) => title.language === language);
    if (germanTitle) {
      return germanTitle.text || profile.id;
    }

    // Fallback to title with no language or empty language
    const fallbackTitle = profile.titles.find(
      (title) => title.language === null || title.language === "" || title.language === undefined
    );
    
    if (fallbackTitle) {
      return fallbackTitle.text;
    }

    // Final fallback to profile ID
    return profile.id;
  };

  useEffect(() => {
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
    loadOrganisations();
    loadValidators();
  }, [id, loadValidators, loadMandate, loadOrganisations]);

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
            <FormSelect
              fieldName={"interlisValidationProfile"}
              label={"validationProfile"}
              required={false}
              selected={mandate?.interlisValidationProfile}
              values={getInterlisProfileSelectMenuItems()}
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
