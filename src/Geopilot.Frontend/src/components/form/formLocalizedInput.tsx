import { FC } from "react";
import { FieldValues } from "react-hook-form";
import { LocalizedText } from "../../api/apiInterfaces";
import { Language } from "../../appInterfaces";
import { FormInput } from "./formInput";

interface FormLocalizedInputProps {
  fieldName: string;
  label: string;
  activeLanguage: Language;
  value?: LocalizedText;
  multiline?: boolean;
  minRows?: number;
  maxRows?: number;
  /** When set, the field must be filled in at least one language. */
  requireAtLeastOne?: boolean;
  helperText?: string;
}

export const FormLocalizedInput: FC<FormLocalizedInputProps> = ({
  fieldName,
  label,
  activeLanguage,
  value,
  multiline,
  minRows,
  maxRows,
  requireAtLeastOne,
  helperText,
}) => {
  const validateAtLeastOne = (_: string, formValues: FieldValues): string | boolean => {
    const entries = (formValues[fieldName] ?? {}) as LocalizedText;
    const anyFilled = Object.values(entries).some(entry => entry.trim() !== "");
    return anyFilled || "atLeastOneLanguageRequired";
  };

  return (
    <>
      {Object.values(Language).map(language => (
        <FormInput
          key={language}
          fieldName={`${fieldName}.${language}`}
          label={label}
          value={value?.[language]}
          multiline={multiline}
          minRows={minRows}
          maxRows={maxRows}
          sx={{ display: language === activeLanguage ? "inherit" : "none" }}
          showRequiredIndicator={requireAtLeastOne}
          validate={requireAtLeastOne ? validateAtLeastOne : undefined}
          deps={
            requireAtLeastOne
              ? Object.values(Language)
                  .filter(other => other !== language)
                  .map(other => `${fieldName}.${other}`)
              : undefined
          }
          helperText={helperText}
        />
      ))}
    </>
  );
};
