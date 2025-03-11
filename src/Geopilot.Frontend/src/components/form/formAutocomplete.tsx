import { Autocomplete, Chip, SxProps, TextField } from "@mui/material";
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
  displayText: string;
  fullDisplayText?: string;
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

  const mapToFormAutocompleteValue = <T extends { id: number }>(
    items: T[] | undefined,
    getDisplayText: (item: T) => string,
    getFullDisplayText: (item: T) => string,
  ): FormAutocompleteValue[] => {
    return (
      items?.map(item => ({
        id: item.id,
        displayText: getDisplayText(item),
        fullDisplayText: getFullDisplayText(item),
      })) || []
    );
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
          renderTags={(tagValue, getTagProps) =>
            tagValue.map((option, index) => {
              const label = typeof option === "string" ? option : option.displayText || "";
              const key = typeof option === "string" ? option : option.id;

              return <Chip {...getTagProps({ index })} key={key} label={label} />;
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
          getOptionKey={(option: string | FormAutocompleteValue) =>
            typeof option === "string"
              ? `${option}-${(values as string[])?.indexOf(option as string)}`
              : `${String(option.id)}-${(values as FormAutocompleteValue[])?.indexOf(option as FormAutocompleteValue)}`
          }
          getOptionLabel={(option: string | FormAutocompleteValue) =>
            typeof option === "string"
              ? option
              : (option as FormAutocompleteValue).fullDisplayText || (option as FormAutocompleteValue).displayText
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
