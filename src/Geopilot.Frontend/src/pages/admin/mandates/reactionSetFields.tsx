import { FC, Fragment, ReactElement } from "react";
import { useWatch } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Typography } from "@mui/material";
import { MandateFormValues } from "../../../api/apiInterfaces.ts";
import { MandateField, MandateValueType, ReactionSetSummary } from "../../../api/generated";
import {
  FormCheckbox,
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
  reactionSets: ReactionSetSummary[];
}

const parseEntry = (input: string) => input.trim() || undefined;

/** Groups the fields of a set by their reaction, keeping the order in which the set lists its reactions. */
const groupByReaction = (fields: MandateField[]) => [
  ...fields
    .reduce(
      (groups, field) => groups.set(field.reactionId, [...(groups.get(field.reactionId) ?? []), field]),
      new Map<string, MandateField[]>(),
    )
    .values(),
];

/**
 * The reaction set of a mandate and the values the chosen set asks the mandate for, grouped under the name of their
 * reaction. The page shows it only where a mandate can choose a set, and only once the sets are known, so the choice
 * mounts with the form and always submits what the mandate holds.
 */
const ReactionSetFields: FC<ReactionSetFieldsProps> = ({ mandate, reactionSets }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const selectedId = useWatch({ name: "reactionSetId", defaultValue: mandate?.reactionSetId ?? "" });
  const selectedSet = reactionSets.find(set => set.id === selectedId);

  const menuItems: FormSelectValue[] = reactionSets.map((set, idx) => {
    const name = localized(set.displayName, set.id);
    return { key: idx, value: set.id, name: name === set.id ? set.id : `${name} (${t("id")}: ${set.id})` };
  });

  const renderInput = (field: MandateField, fieldName: string, label: ReactElement) => {
    const stored = mandate?.reactionSetValues?.[field.reactionId]?.[field.key];

    switch (field.type) {
      case MandateValueType.Switch:
        // A switch always has a value, so it is never required: unset means off.
        return <FormCheckbox fieldName={fieldName} label={label} checked={stored === true} />;
      case MandateValueType.TextList:
        return (
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
        );
      default:
        return (
          <FormInput
            fieldName={fieldName}
            label={label}
            required={field.required}
            value={typeof stored === "string" ? stored : undefined}
          />
        );
    }
  };

  const renderField = (field: MandateField, fullWidth: boolean) => {
    const fieldName = `reactionSetValues.${field.reactionId}.${field.key}`;
    // The label comes from the pipeline definition, already in the active language, so it goes to the form components
    // as an element, which they show as it is instead of looking it up as a translation key.
    const input = renderInput(field, fieldName, <>{localized(field.displayName, field.key)}</>);

    return fullWidth ? (
      <Fragment key={fieldName}>{input}</Fragment>
    ) : (
      <FormContainerHalfWidth key={fieldName}>{input}</FormContainerHalfWidth>
    );
  };

  return (
    <>
      <FormContainer>
        <FormSelect
          fieldName={"reactionSetId"}
          label={"reactionSet"}
          required={false}
          values={menuItems}
          selected={mandate?.reactionSetId ?? undefined}
        />
      </FormContainer>
      {groupByReaction(selectedSet?.mandateFields ?? []).map(fields => (
        <Fragment key={fields[0].reactionId}>
          <Typography variant={"body1"}>{localized(fields[0].reactionDisplayName, fields[0].reactionId)}</Typography>
          {/* A reaction that asks for a single value gives it the whole width. */}
          <FormContainer sx={{ flexWrap: "wrap" }}>
            {fields.map(field => renderField(field, fields.length === 1))}
          </FormContainer>
        </Fragment>
      ))}
    </>
  );
};

export default ReactionSetFields;
