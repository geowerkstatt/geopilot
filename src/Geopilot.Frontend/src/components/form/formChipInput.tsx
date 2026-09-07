import { FC, KeyboardEvent, useState } from "react";
import { useController, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Stack, TextField } from "@mui/material";
import { SelectedChips } from "./selectedChips";

export interface FormChipInputProps {
  fieldName: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  /** The values the field starts with, typically the stored ones. */
  selected?: string[];
  /**
   * Interprets the typed text: returns the value to add, or undefined to reject the input and show `errorMessage`.
   * Normalization such as trimming, casing or a missing prefix belongs here, so that what becomes a chip is also
   * what gets stored.
   */
  parse: (input: string) => string | undefined;
  /** Translation key of the message shown when `parse` rejects the input. */
  errorMessage: string;
}

/** Keys that confirm the typed text. The comma allows a natural writing flow next to the explicit Enter. */
const confirmKeys = ["Enter", ","];

/** Collects free text values as removable chips below the input. Every value stays visible. */
export const FormChipInput: FC<FormChipInputProps> = ({
  fieldName,
  label,
  placeholder,
  required,
  disabled,
  selected,
  parse,
  errorMessage,
}) => {
  const { t } = useTranslation();
  const { control, setError, clearErrors } = useFormContext();
  const { field, fieldState } = useController({
    name: fieldName,
    control,
    defaultValue: selected ?? [],
    rules: { required: required ?? false },
  });
  const [input, setInput] = useState("");

  const entries: string[] = field.value ?? [];

  const confirmInput = () => {
    const parsed = parse(input);

    if (!parsed) {
      setError(fieldName, { type: "validate", message: errorMessage });
      return;
    }

    clearErrors(fieldName);
    setInput("");

    if (!entries.includes(parsed)) {
      field.onChange([...entries, parsed]);
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (!confirmKeys.includes(event.key) || !input.trim()) return;

    event.preventDefault();
    confirmInput();
  };

  return (
    <Stack spacing={1} sx={{ width: "100%" }}>
      <TextField
        label={t(label)}
        placeholder={placeholder ? t(placeholder) : undefined}
        required={required ?? false}
        disabled={disabled ?? false}
        error={!!fieldState.error}
        helperText={fieldState.error?.message ? t(fieldState.error.message) : undefined}
        value={input}
        onChange={event => {
          setInput(event.target.value);
          // Only the rejection belongs to the text being edited. A missing mandatory value stays reported.
          if (fieldState.error?.type === "validate") {
            clearErrors(fieldName);
          }
        }}
        onBlur={() => {
          // Confirm on the way out as well, so leaving the field does not silently discard a typed value.
          if (input.trim()) {
            confirmInput();
          }
          field.onBlur();
        }}
        onKeyDown={handleKeyDown}
        data-cy={`${fieldName}-formChipInput`}
      />
      {entries.length > 0 && (
        <SelectedChips
          values={entries}
          onDelete={index => field.onChange(entries.filter((_, i) => i !== index))}
          dataCy={`${fieldName}-selectedChips`}
        />
      )}
    </Stack>
  );
};
