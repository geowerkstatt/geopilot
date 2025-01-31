import { Autocomplete, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { FC, SyntheticEvent, useEffect } from "react";
import { getFormFieldError } from "./form.ts";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";

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

  useEffect(() => {
    setValue(fieldName, selected ?? [], { shouldValidate: true, shouldDirty: false });
    // We only want to set the value manually if the selected value changes
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selected]);

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
          onChange={(event: SyntheticEvent, newValue: (string | FormAutocompleteValue)[]) => {
            setValue(fieldName, newValue, { shouldValidate: true, shouldDirty: true });
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
          options={values || []}
          getOptionLabel={(option: FormAutocompleteValue | string) =>
            typeof option === "string" ? option : (option as FormAutocompleteValue).name
          }
          isOptionEqualToValue={(option, value) =>
            typeof option === "string" ? option === value : option.key === (value as FormAutocompleteValue).key
          }
          value={field.value}
          data-cy={fieldName + "-formAutocomplete"}
        />
      )}
    />
  );
};
