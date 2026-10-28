import { FC } from "react";
import { useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { MandateField, MandateValueType, ReactionSetSummary } from "../../../api/generated";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import {
  FormChipInput,
  FormContainer,
  FormContainerHalfWidth,
  FormInput,
  FormSelect,
} from "../../../components/form/form.ts";
import { FormSelectValue } from "../../../components/form/formSelect.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";

interface ReactionSetFieldsProps {
  mandate?: MandateFormValues;
  reactionSets?: ReactionSetSummary[];
}

const parseEntry = (input: string) => input.trim() || undefined;

/**
 * The reaction set of a mandate and the values the chosen set asks the mandate for. The choice stays mounted even
 * when it is locked, so the form always submits what the mandate holds.
 */
const ReactionSetFields: FC<ReactionSetFieldsProps> = ({ mandate, reactionSets }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const { reactionSetsEnabled } = useCapabilities();
  const selectedId = useWatch({ name: "reactionSetId", defaultValue: mandate?.reactionSetId ?? "" });
  const selectedSet = reactionSets?.find(set => set.id === selectedId);

  const menuItems: FormSelectValue[] =
    reactionSets?.map((set, idx) => {
      const name = localized(set.displayName, set.id);
      return { key: idx, value: set.id, name: name === set.id ? set.id : `${name} (${t("id")}: ${set.id})` };
    }) ?? [];

  const renderField = (field: MandateField) => {
    const fieldName = `reactionSetValues.${field.reactionId}.${field.key}`;
    const stored = mandate?.reactionSetValues?.[field.reactionId]?.[field.key];
    // The label comes from the pipeline definition, already in the active language, so it goes to the form components
    // as an element, which they show as it is instead of looking it up as a translation key.
    const label = <>{localized(field.displayName, field.key)}</>;

    return (
      <FormContainerHalfWidth key={fieldName}>
        {field.type === MandateValueType.TextList ? (
          <FormChipInput
            fieldName={fieldName}
            label={label}
            required={field.required}
            selected={
              Array.isArray(stored) ? stored.filter((entry): entry is string => typeof entry === "string") : undefined
            }
            parse={parseEntry}
            errorMessage="entryRequiresText"
          />
        ) : (
          <FormInput
            fieldName={fieldName}
            label={label}
            required={field.required}
            value={typeof stored === "string" ? stored : undefined}
          />
        )}
      </FormContainerHalfWidth>
    );
  };

  return (
    <>
      <FormContainer>
        <FormContainerHalfWidth>
          <FormSelect
            fieldName={"reactionSetId"}
            label={"reactionSet"}
            required={false}
            disabled={!reactionSetsEnabled}
            helperText={t(reactionSetsEnabled ? "reactionSetHelperText" : "reactionSetsDisabled")}
            values={menuItems}
            selected={mandate?.reactionSetId ?? undefined}
          />
        </FormContainerHalfWidth>
      </FormContainer>
      {selectedSet && selectedSet.mandateFields.length > 0 && (
        <FormContainer sx={{ flexWrap: "wrap" }}>{selectedSet.mandateFields.map(renderField)}</FormContainer>
      )}
    </>
  );
};

export default ReactionSetFields;
