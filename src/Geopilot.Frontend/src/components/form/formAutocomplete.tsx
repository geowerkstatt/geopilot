import { Autocomplete, Chip, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { SyntheticEvent, useMemo, useState } from "react";
import { getFormFieldError } from "./form";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

export interface FormAutocompleteProps<T> {
  fieldName: string;
  label: string;
  placeholder?: string;
  freeSolo?: boolean;
  required?: boolean;
  disabled?: boolean;
  selected?: T[];
  values?: T[];
  /**
   * Method that formats non-string values to a format that can be displayed in the autocomplete.
   * This method is mandatory for non-string values, the application will crash if it is not provided.
   *
   * Usage:
   * valueFormatter={(value) => ({
   *   id: value.id,
   *   primaryText: value.name,
   *   detailText: value.description
   * })}
   */
  valueFormatter?: (value: T) => FormAutocompleteValue;
  sx?: SxProps;

  /**
   * When using freeSolo, validate the typed string.
   * Return true to accept, false to reject it as a chip.
   */
  validator?: (value: string) => boolean;

  /**
   * Error message key/text when the validation fails.
   */
  errorMessage?: string;
}

export interface FormAutocompleteValue {
  id: number;
  /**
   * Primary text displayed in both the chip and dropdown.
   */
  primaryText: string;
  /**
   * Extended text displayed only in the dropdown for additional context.
   * When provided, this will be shown in the dropdown instead of primaryText.
   * If not provided, primaryText will be used in the dropdown as well.
   */
  detailText?: string;
}

export const FormAutocomplete = <T,>({
  fieldName,
  label,
  placeholder,
  freeSolo,
  required,
  disabled,
  selected,
  values,
  valueFormatter,
  sx,
  validator,
  errorMessage,
}: FormAutocompleteProps<T>) => {
  const { t } = useTranslation();
  const { control, setValue, setError, clearErrors } = useFormContext();

  const [inputValue, setInputValue] = useState("");

  const safeValueFormatter = useMemo(
    () =>
      (option: T): FormAutocompleteValue => {
        if (!valueFormatter) {
          throw new Error(`Missing valueFormatter for non-string option in FormAutocomplete "${fieldName}"`);
        }
        const formatted = valueFormatter(option);

        if (formatted.id === undefined || formatted.id === null) {
          throw new Error(`Missing mandatory ID property for non-strings in FormAutocomplete  "${fieldName}"`);
        }

        return formatted;
      },
    [fieldName, valueFormatter],
  );

  const onChange = (event: SyntheticEvent, newValue: (T | string)[]) => {
    const last = newValue[newValue.length - 1];

    if (freeSolo && typeof last === "string" && validator) {
      const isValid = validator(last);

      if (!isValid) {
        // Reject this one: remove from chips, keep it in the text field, set error
        const filtered = newValue.filter(v => v !== last);

        setInputValue(last);

        setValue(fieldName, filtered, {
          shouldValidate: false,
          shouldDirty: true,
          shouldTouch: true,
        });

        setError(fieldName, {
          type: "validate",
          message: errorMessage || "",
        });

        return;
      }

      // Accepted: clear error and clear input
      clearErrors(fieldName);
      setInputValue("");
    }

    setValue(fieldName, newValue, {
      shouldValidate: true,
      shouldDirty: true,
      shouldTouch: true,
    });
  };

  return (
    <Controller
      name={fieldName}
      control={control}
      defaultValue={selected ?? []}
      rules={{
        required: required ?? false,
      }}
      render={({ field, formState }) => (
        <Autocomplete
          sx={{ ...sx }}
          fullWidth={true}
          size={"small"}
          popupIcon={<ExpandMoreIcon />}
          multiple
          freeSolo={freeSolo ?? false}
          disabled={disabled ?? false}
          value={field.value}
          inputValue={inputValue}
          onInputChange={(_, newInputValue) => {
            setInputValue(newInputValue);
            if (!newInputValue) {
              clearErrors(fieldName);
            }
          }}
          onChange={onChange}
          renderTags={(value, getTagProps) =>
            value.map((option, index) => {
              const isStr = typeof option === "string";
              return (
                <Chip
                  {...getTagProps({ index: index })}
                  key={isStr ? option : safeValueFormatter(option as T).id}
                  label={isStr ? option : safeValueFormatter(option as T).primaryText}
                />
              );
            })
          }
          renderInput={params => (
            <TextField
              {...params}
              label={t(label)}
              placeholder={placeholder ? t(placeholder) : undefined}
              required={required ?? false}
              error={getFormFieldError(fieldName, formState.errors)}
              helperText={
                formState.errors[fieldName]?.message ? t(formState.errors[fieldName]?.message as string) : undefined
              }
            />
          )}
          options={values || []}
          getOptionKey={(option: T | string) =>
            typeof option === "string" ? `${fieldName}-${option}` : `${fieldName}-${(values as T[]).indexOf(option)}`
          }
          getOptionLabel={(option: T | string) =>
            typeof option === "string"
              ? option
              : safeValueFormatter(option as T).detailText || safeValueFormatter(option as T).primaryText
          }
          isOptionEqualToValue={(option, value) =>
            typeof option === "string"
              ? (option as string) === (value as string)
              : safeValueFormatter(option as T).id === safeValueFormatter(value as T).id
          }
          data-cy={fieldName + "-formAutocomplete"}
        />
      )}
    />
  );
};
