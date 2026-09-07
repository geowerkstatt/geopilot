import { FC, KeyboardEvent, useRef, useState } from "react";
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
  const { control, trigger } = useFormContext();
  // A ref rather than state, because react-hook-form reads the rules once when the field is registered: the rule
  // below has to look the current value up instead of closing over it.
  const rejectedInput = useRef(false);
  const { field, fieldState } = useController({
    name: fieldName,
    control,
    defaultValue: selected ?? [],
    rules: {
      /**
       * Rejected text has to make the field invalid itself, so the form keeps the error and refuses to save while
       * text that was never taken in still sits in the field. An error set from the outside does not survive:
       * react-hook-form drops a foreign error the next time it validates the field, which a blur does. One rule
       * covers both cases so a rejection is still explained while the field holds no value yet.
       */
      validate: (value: string[]) => {
        if (rejectedInput.current) {
          return errorMessage;
        }

        return !required || (value ?? []).length > 0;
      },
    },
  });
  const [input, setInput] = useState("");

  const entries: string[] = field.value ?? [];

  const setRejected = (rejected: boolean) => {
    if (rejectedInput.current === rejected) return;

    rejectedInput.current = rejected;
    trigger(fieldName);
  };

  /** Shows the text and drops a pending rejection, because the text it was about is being edited. */
  const showInput = (text: string) => {
    setInput(text);
    setRejected(false);
  };

  /**
   * Appends the candidates the field does not hold yet. Compares case insensitively, since values stored before
   * `parse` normalized them keep the casing they were saved with.
   */
  const addEntries = (candidates: string[]) => {
    const known = entries.map(entry => entry.toLowerCase());
    const added = candidates.filter(
      (candidate, index) => candidates.indexOf(candidate) === index && !known.includes(candidate),
    );

    if (added.length > 0) {
      field.onChange([...entries, ...added]);
    }
  };

  const confirmInput = () => {
    const parsed = parse(input);

    if (!parsed) {
      setRejected(true);
      return;
    }

    showInput("");
    addEntries([parsed]);
  };

  /**
   * Takes in what the user separated with a comma and leaves the rest in the field. Handles a typed comma and a
   * pasted list alike, because pasting fires no key event at all.
   */
  const handleInput = (text: string) => {
    const parts = text.split(",");
    const remainder = parts.pop() ?? "";
    const candidates = parts.filter(part => part.trim().length > 0);

    if (candidates.length === 0) {
      showInput(remainder);
      return;
    }

    const parsed: string[] = [];

    for (const candidate of candidates) {
      const value = parse(candidate);

      if (!value) {
        // Keep the whole text, so that what was typed or pasted can be corrected instead of being thrown away.
        setInput(text);
        setRejected(true);
        return;
      }

      parsed.push(value);
    }

    showInput(remainder);
    addEntries(parsed);
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== "Enter" || !input.trim()) return;

    // Enter would submit the form, and here it confirms the entry instead.
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
        onChange={event => handleInput(event.target.value)}
        onBlur={field.onBlur}
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
