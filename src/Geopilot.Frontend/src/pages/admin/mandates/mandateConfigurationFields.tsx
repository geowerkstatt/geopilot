import { FC } from "react";
import { useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { FormHelperText } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { Organisation, PipelineSummary } from "../../../api/generated";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
} from "../../../components/form/form.ts";
import PipelineFormSelect from "./pipelineFormSelect.tsx";

interface MandateConfigurationFieldsProps {
  mandate?: MandateFormValues;
  organisations?: Organisation[];
  pipelines?: PipelineSummary[];
}

const MandateConfigurationFields: FC<MandateConfigurationFieldsProps> = ({ mandate, organisations, pipelines }) => {
  const { t } = useTranslation();
  const isPublic = useWatch({ name: "isPublic", defaultValue: mandate?.isPublic ?? false });

  return (
    <>
      <FormContainer>
        <FormContainerHalfWidth>
          <PipelineFormSelect pipelines={pipelines} selected={mandate?.pipelineId ?? undefined} />
        </FormContainerHalfWidth>
        <FormContainerHalfWidth>
          <FormAutocomplete<string>
            freeSolo
            validator={v => /^\.(\*|[a-zA-Z0-9]+)$/i.test(v)}
            errorMessage="invalidFileExtension"
            fieldName={"fileTypes"}
            label={"fileTypes"}
            required={true}
            values={[]}
            selected={mandate?.fileTypes}
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
