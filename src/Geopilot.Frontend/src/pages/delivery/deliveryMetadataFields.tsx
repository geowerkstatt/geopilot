import { FC } from "react";
import { FieldEvaluationType, MetadataField, MetadataFieldType } from "../../api/generated";
import { FormChipInput, FormContainer, FormInput, FormSelect, FormValueType } from "../../components/form/form.ts";
import { useLocalized } from "../../hooks/useLocalized.ts";

interface DeliveryMetadataFieldsProps {
  fields: MetadataField[];
  disabled: boolean;
}

const parseEntry = (input: string) => input.trim() || undefined;

/** The additional fields a mandate asks for, one input per field in the order the mandate lists them. */
export const DeliveryMetadataFields: FC<DeliveryMetadataFieldsProps> = ({ fields, disabled }) => {
  const { localized } = useLocalized();

  // An empty number input reads as NaN, which react-hook-form does not always treat as empty, so the check is explicit.
  // FormInput translates the returned key.
  const validateNumber = (field: MetadataField) => (value: string | number) => {
    if (typeof value !== "number" || Number.isNaN(value)) {
      return field.evaluation !== FieldEvaluationType.Required;
    }
    return field.type === MetadataFieldType.Integer && !Number.isInteger(value) ? "metadataWholeNumber" : true;
  };

  const renderInput = (field: MetadataField) => {
    const fieldName = `metadata.${field.id}`;
    // The title comes from the mandate, already in the active language, so it goes to the form components as an
    // element, which they show as it is instead of looking it up as a translation key.
    const label = <>{localized(field.title, field.id)}</>;
    const required = field.evaluation === FieldEvaluationType.Required;

    switch (field.type) {
      case MetadataFieldType.Text:
        return <FormInput fieldName={fieldName} label={label} required={required} disabled={disabled} />;
      case MetadataFieldType.TextList:
        return (
          <FormChipInput
            fieldName={fieldName}
            label={label}
            required={required}
            disabled={disabled}
            parse={parseEntry}
            errorMessage="entryRequiresText"
          />
        );
      case MetadataFieldType.Selection:
        return (
          <FormSelect
            fieldName={fieldName}
            label={label}
            required={required}
            disabled={disabled}
            values={(field.options ?? []).map((option, index) => ({
              key: index,
              value: option.id,
              name: localized(option.title, option.id),
            }))}
          />
        );
      case MetadataFieldType.Integer:
      case MetadataFieldType.Decimal:
        return (
          <FormInput
            fieldName={fieldName}
            label={label}
            type={FormValueType.Number}
            required={required}
            disabled={disabled}
            validate={validateNumber(field)}
          />
        );
      case MetadataFieldType.Date:
        return (
          <FormInput
            fieldName={fieldName}
            label={label}
            type={FormValueType.Date}
            required={required}
            disabled={disabled}
          />
        );
      default: {
        const unhandled: never = field.type;
        throw new Error(`Unknown metadata field type: ${String(unhandled)}`);
      }
    }
  };

  return (
    <>
      {fields.map(field => (
        <FormContainer key={field.id}>{renderInput(field)}</FormContainer>
      ))}
    </>
  );
};
