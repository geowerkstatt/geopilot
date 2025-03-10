import { Autocomplete, SxProps, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { FC, SyntheticEvent } from "react";
import { getFormFieldError } from "./form";
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
  id: number;
  name?: string;
  fullName?: string;
  email?: string;
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

  const formatOptionLabel = (option: FormAutocompleteValue | string): string => {
    if (typeof option === "string") {
      return option;
    }

    if (option.name) {
      return `${option.name} (ID: ${option.id})`;
    }

    if (option.fullName) {
      return `${option.fullName} (${option.email})`;
    }

    return "";
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
          onChange={(event: SyntheticEvent, newValue: (string | FormAutocompleteValue)[]) =>
            setValue(fieldName, newValue, { shouldValidate: true, shouldDirty: true, shouldTouch: true })
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
          getOptionKey={(option: string | FormAutocompleteValue) =>
            typeof option === "string" ? option : String(option.id)
          }
          getOptionLabel={(option: FormAutocompleteValue | string) =>
            typeof option === "string"
              ? option
              : (option as FormAutocompleteValue).name || (option as FormAutocompleteValue).fullName || ""
          }
          isOptionEqualToValue={(option, value) =>
            typeof option === "string" ? option === value : option.id === (value as FormAutocompleteValue).id
          }
          value={field.value}
          data-cy={fieldName + "-formAutocomplete"}
        />
      )}
    />
  );
};
