import { FC, useEffect, useState } from "react";
import { useFormContext, useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { FormHelperText } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { Organisation, PipelineSummary } from "../../../api/generated";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import {
  FormAutocomplete,
  FormCheckbox,
  FormChipInput,
  FormContainer,
  FormContainerHalfWidth,
  FormInput,
} from "../../../components/form/form.ts";
import useFetch from "../../../hooks/useFetch.ts";
import PipelineFormSelect from "./pipelineFormSelect.tsx";

interface MandateConfigurationFieldsProps {
  mandate?: MandateFormValues;
  organisations?: Organisation[];
  pipelines?: PipelineSummary[];
}

const fileTypePattern = /^\.(\*|[a-z0-9]+)$/;

/**
 * Accepts "xtf" as readily as ".XTF": the period is optional and the value is stored in lower case, which is what
 * the mandate lookup compares against anyway. A bare "*" turns into the ".*" wildcard that accepts every format.
 */
const parseFileType = (input: string): string | undefined => {
  const trimmed = input.trim().toLowerCase();
  const withPeriod = trimmed.startsWith(".") ? trimmed : `.${trimmed}`;

  return fileTypePattern.test(withPeriod) ? withPeriod : undefined;
};

const MandateConfigurationFields: FC<MandateConfigurationFieldsProps> = ({ mandate, organisations, pipelines }) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { trigger } = useFormContext();
  const { machineDeliveryEnabled } = useCapabilities();
  const isPublic = useWatch({ name: "isPublic", defaultValue: mandate?.isPublic ?? false });
  const [usedKeys, setUsedKeys] = useState<string[]>();

  useEffect(() => {
    if (!machineDeliveryEnabled) return;

    fetchApi<string[]>("/api/v1/mandate/keys", { errorMessageLabel: "mandateKeysLoadingError" })
      .then(setUsedKeys)
      .catch(() => setUsedKeys([]));
  }, [fetchApi, machineDeliveryEnabled]);

  // The keys arrive after the first render, so a key typed in the meantime was checked against nothing.
  // Without machine delivery they never arrive and the field is not rendered, so this stays idle.
  useEffect(() => {
    if (usedKeys !== undefined) trigger("key");
  }, [usedKeys, trigger]);

  const validateUniqueKey = (value: string) => {
    // Trimmed like the backend does before it compares, so a stray space cannot pass here and still
    // collide once the server has normalized it. Optional chaining despite the typing: react-hook-form
    // hands a field the user never touched as undefined, and throwing here would abort the submit.
    const trimmed = value?.trim();
    if (trimmed && trimmed !== mandate?.key && usedKeys?.includes(trimmed)) {
      return "mandateKeyNotUnique";
    }
    return true;
  };

  return (
    <>
      {machineDeliveryEnabled && (
        <FormContainer>
          <FormInput
            label="mandateKey"
            fieldName="key"
            value={mandate?.key ?? ""}
            helperText={t("mandateKeyHelperText")}
            validate={validateUniqueKey}
          />
        </FormContainer>
      )}
      <FormContainer>
        <FormContainerHalfWidth>
          <PipelineFormSelect pipelines={pipelines} selected={mandate?.pipelineId ?? undefined} />
        </FormContainerHalfWidth>
        <FormContainerHalfWidth>
          <FormChipInput
            fieldName={"fileTypes"}
            label={"fileTypes"}
            placeholder={"fileTypesPlaceholder"}
            required={true}
            selected={mandate?.fileTypes}
            parse={parseFileType}
            errorMessage="invalidFileExtension"
          />
        </FormContainerHalfWidth>
      </FormContainer>
      <FormContainer sx={{ alignItems: "center" }}>
        <FormCheckbox fieldName={"isPublic"} label={"public"} checked={mandate?.isPublic ?? false} />
        {isPublic && <FormHelperText>{t("publicMandateHelperText")}</FormHelperText>}
      </FormContainer>
      {!isPublic && (
        <FormContainer>
          <FormContainerHalfWidth>
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
          </FormContainerHalfWidth>
          <FormCheckbox fieldName={"allowDelivery"} label={"allowDelivery"} checked={mandate?.allowDelivery ?? false} />
        </FormContainer>
      )}
    </>
  );
};

export default MandateConfigurationFields;
