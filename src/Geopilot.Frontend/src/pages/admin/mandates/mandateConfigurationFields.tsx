import { FC } from "react";
import { useWatch } from "react-hook-form";
import { Mandate, Organisation, PipelineSummary } from "../../../api/apiInterfaces.ts";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
} from "../../../components/form/form.ts";
import PipelineFormSelect from "./pipelineFormSelect.tsx";

interface MandateConfigurationFieldsProps {
  mandate?: Mandate;
  organisations?: Organisation[];
  pipelines?: PipelineSummary[];
}

const MandateConfigurationFields: FC<MandateConfigurationFieldsProps> = ({ mandate, organisations, pipelines }) => {
  const isPublic = useWatch({ name: "isPublic", defaultValue: mandate?.isPublic ?? false });

  return (
    <>
      <FormContainer>
        <FormContainerHalfWidth>
          <PipelineFormSelect pipelines={pipelines} selected={mandate?.pipelineId} />
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
      <FormContainer>
        <FormCheckbox fieldName={"isPublic"} label={"public"} checked={mandate?.isPublic ?? false} />
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
