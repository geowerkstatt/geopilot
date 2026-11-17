import { FC } from "react";
import { FieldValues, get } from "react-hook-form";
import { LocalizedText } from "../../api/apiInterfaces";
import { Language, TranslationKey } from "../../appInterfaces";
import { FormInput } from "./formInput";

interface FormLocalizedInputProps {
  fieldName: string;
  label: TranslationKey;
  activeLanguage: Language;
  value?: LocalizedText;
  multiline?: boolean;
  minRows?: number;
  maxRows?: number;
  /** When set, the field must be filled in at least one language. */
  requireAtLeastOne?: boolean;
  /**
   * A further check of the text in all its languages. Every language input shows its message, so it stays in sight
   * whichever language is active.
   */
  validate?: (text: LocalizedText) => string | boolean;
  /** Field names that are checked again whenever the text changes, for checks that span several fields. */
  deps?: string[];
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
  validate,
  deps,
  helperText,
}) => {
  const checksText = requireAtLeastOne || validate !== undefined;

  const validateText = (_: string, formValues: FieldValues): string | boolean => {
    const entries = (get(formValues, fieldName) ?? {}) as LocalizedText;
    const anyFilled = Object.values(entries).some(entry => typeof entry === "string" && entry.trim() !== "");
    if (requireAtLeastOne && !anyFilled) {
      return "atLeastOneLanguageRequired";
    }
    return validate ? validate(entries) : true;
  };

  // A check of the whole text runs on every language input, so a change in one language checks the others again.
  const otherLanguages = (language: Language) =>
    Object.values(Language)
      .filter(other => other !== language)
      .map(other => `${fieldName}.${other}`);

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
          validate={checksText ? validateText : undefined}
          deps={checksText ? [...otherLanguages(language), ...(deps ?? [])] : deps}
          helperText={helperText}
        />
      ))}
    </>
  );
};
