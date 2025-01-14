import { Autocomplete, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { FC, SyntheticEvent, useMemo } from "react";
import { getFormFieldError } from "./form.ts";

export interface FormAutocompleteProps {
  fieldName: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  selected?: FormAutocompleteValue[] | string[];
  values?: FormAutocompleteValue[] | string[];
  sx?: SxProps;
}

export interface FormAutocompleteValue {
  key: number;
  name: string;
}

export const FormAutocomplete: FC<FormAutocompleteProps> = ({
  fieldName,
  label,
  placeholder,
  required,
  disabled,
  selected,
  values,
  sx,
}) => {
  const { t } = useTranslation();
  const { control, setValue } = useFormContext();

  const convertedValues = useMemo(() => {
    if (values && typeof values[0] === "string") {
      return (values as string[]).map((value, index) => ({ key: index, name: value }));
    }
    return values as FormAutocompleteValue[];
  }, [values]);

  const convertedSelected = useMemo(() => {
    if (selected && typeof selected[0] === "string") {
      return (selected as string[]).map(value => {
        const matchingValue = convertedValues.find(val => val.name === value);
        return matchingValue ? { key: matchingValue.key, name: value } : { key: -1, name: value };
      });
    }
    return selected as FormAutocompleteValue[];
  }, [convertedValues, selected]);

  return (
    <Controller
      name={fieldName}
      control={control}
      defaultValue={convertedSelected ?? []}
      rules={{
        required: required ?? false,
      }}
      render={({ field, formState }) => (
        <Autocomplete
          sx={{ ...sx }}
          fullWidth={true}
          size={"small"}
          multiple
          disabled={disabled ?? false}
          onChange={(event: SyntheticEvent, newValue: FormAutocompleteValue[]) => {
            setValue(fieldName, newValue, { shouldValidate: true });
          }}
          renderInput={params => (
            <TextField
              {...params}
              label={t(label)}
              placeholder={placeholder ? t(placeholder) : undefined}
              required={required ?? false}
              error={getFormFieldError(fieldName, formState.errors)}
            />
          )}
          options={convertedValues || []}
          getOptionLabel={(option: FormAutocompleteValue) => option.name}
          isOptionEqualToValue={(option, value) => option.key === value.key}
          value={field.value}
          data-cy={fieldName + "-formAutocomplete"}
        />
      )}
    />
  );
};
