import { Autocomplete, Chip, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { SyntheticEvent } from "react";
import { getFormFieldError } from "./form";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

export interface FormAutocompleteProps<T> {
  fieldName: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  selected?: T[];
  values?: T[];
  valueFormatter?: (value: T) => FormAutocompleteValue;
  sx?: SxProps;
}

export interface FormAutocompleteValue {
  id: number;
  displayText: string;
  fullDisplayText?: string;
}

export const FormAutocomplete = <T,>({
  fieldName,
  label,
  placeholder,
  required,
  disabled,
  selected,
  values,
  valueFormatter,
  sx,
}: FormAutocompleteProps<T>) => {
  const { t } = useTranslation();
  const { control, setValue } = useFormContext();

  const safeValueFormatter = (option: T): FormAutocompleteValue => {
    if (!valueFormatter) {
      throw new Error(`Missing valueFormatter for non-string option in FormAutocomplete "${fieldName}"`);
    }
    const formatted = valueFormatter(option);

    if (formatted.id === undefined || formatted.id === null) {
      throw new Error(`Missing mandatory ID property for non-strings in FormAutocomplete  "${fieldName}"`);
    }

    return formatted;
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
          disabled={disabled ?? false}
          onChange={(event: SyntheticEvent, newValue: T[]) =>
            setValue(fieldName, newValue, { shouldValidate: true, shouldDirty: true, shouldTouch: true })
          }
          renderTags={(value, getTagProps) =>
            value.map((option, index) => {
              const isStr = typeof option === "string";
              return (
                <Chip
                  {...getTagProps({ index: index })}
                  key={isStr ? option : safeValueFormatter(option as T).id}
                  label={isStr ? option : safeValueFormatter(option as T).displayText}
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
            />
          )}
          options={values || []}
          getOptionKey={(option: T) => (values as T[]).indexOf(option)}
          getOptionLabel={(option: T) =>
            typeof option === "string"
              ? option
              : safeValueFormatter(option as T).fullDisplayText || safeValueFormatter(option as T).displayText
          }
          isOptionEqualToValue={(option, value) =>
            typeof option === "string"
              ? (option as string) === (value as string)
              : safeValueFormatter(option as T).id === safeValueFormatter(value as T).id
          }
          value={field.value}
          data-cy={fieldName + "-formAutocomplete"}
        />
      )}
    />
  );
};
