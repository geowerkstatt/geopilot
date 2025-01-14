import { Autocomplete, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useFormContext } from "react-hook-form";
import { FC, SyntheticEvent } from "react";
import { getFormFieldError } from "./form.ts";

export interface FormAutocompleteProps {
  fieldName: string;
  label: string;
  placeholder?: string;
  required?: boolean;
  disabled?: boolean;
  selected?: FormAutocompleteValue[];
  values?: FormAutocompleteValue[];
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
  const { formState, register, setValue } = useFormContext();

  return (
    <Autocomplete
      sx={{ width: "100%", ...sx }}
      multiple
      size="small"
      {...register(fieldName, {
        required: required || false,
      })}
      onChange={(event: SyntheticEvent, newValue: FormAutocompleteValue[]) => {
        setValue(fieldName, newValue, { shouldValidate: true });
      }}
      disabled={disabled || false}
      options={values || []}
      getOptionLabel={(option: FormAutocompleteValue) => option.name}
      defaultValue={selected || []}
      renderInput={params => (
        <TextField
          {...params}
          label={t(label)}
          placeholder={placeholder ? t(placeholder) : undefined}
          required={required ?? false}
          error={getFormFieldError(fieldName, formState.errors)}
        />
      )}
      data-cy={fieldName + "-formAutocomplete"}
    />
  );
};
