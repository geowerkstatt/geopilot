import { Autocomplete, Chip, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { SyntheticEvent, useMemo } from "react";
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
  required,
  disabled,
  selected,
  values,
  valueFormatter,
  sx,
}: FormAutocompleteProps<T>) => {
  const { t } = useTranslation();
  const { control, setValue } = useFormContext();

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
            />
          )}
          options={values || []}
          getOptionKey={(option: T) => `${fieldName}-${(values as T[]).indexOf(option)}`}
          getOptionLabel={(option: T) =>
            typeof option === "string"
              ? option
              : safeValueFormatter(option as T).detailText || safeValueFormatter(option as T).primaryText
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
