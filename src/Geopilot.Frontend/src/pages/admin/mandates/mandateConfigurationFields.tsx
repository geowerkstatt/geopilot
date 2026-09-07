import { FC } from "react";
import { useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Box, FormHelperText } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { Organisation, PipelineSummary } from "../../../api/generated";
import {
  FormAutocomplete,
  FormCheckbox,
  FormChipInput,
  FormContainer,
  FormContainerHalfWidth,
} from "../../../components/form/form.ts";
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
  const isPublic = useWatch({ name: "isPublic", defaultValue: mandate?.isPublic ?? false });

  return (
    <>
      {/* The formats field spans both rows, leaving room for its chips below the input. Stacks on small screens. */}
      <Box
        sx={{
          display: "grid",
          gap: 2,
          gridTemplateColumns: { xs: "1fr", md: "repeat(2, minmax(0, 1fr))" },
          alignItems: "start",
        }}>
        <FormContainer sx={{ gridColumn: { md: "1" }, gridRow: { md: "1" } }}>
          <PipelineFormSelect pipelines={pipelines} selected={mandate?.pipelineId ?? undefined} />
        </FormContainer>
        <FormContainer sx={{ gridColumn: { md: "2" }, gridRow: { md: "1 / span 2" } }}>
          <FormChipInput
            fieldName={"fileTypes"}
            label={"fileTypes"}
            placeholder={"fileTypesPlaceholder"}
            required={true}
            selected={mandate?.fileTypes}
            parse={parseFileType}
            errorMessage="invalidFileExtension"
          />
        </FormContainer>
        <FormContainer sx={{ alignItems: "center", gridColumn: { md: "1" }, gridRow: { md: "2" } }}>
          <FormCheckbox fieldName={"isPublic"} label={"public"} checked={mandate?.isPublic ?? false} />
          {isPublic && <FormHelperText>{t("publicMandateHelperText")}</FormHelperText>}
        </FormContainer>
      </Box>
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
