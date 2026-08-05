import { FC } from "react";
import { FieldValues, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { InputProps, SxProps, TextField } from "@mui/material";
import { isValid } from "date-fns";
import { FormValueType, getFormFieldError, getFormFieldErrorMessage } from "./form";

export interface FormInputProps {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode, where it only feeds `data-cy`. */
  fieldName?: string;
  label: string;
  required?: boolean;
  /** Marks the label with the required asterisk without making this single field mandatory. */
  showRequiredIndicator?: boolean;
  disabled?: boolean;
  type?: FormValueType;
  multiline?: boolean;
  rows?: number;
  minRows?: number;
  maxRows?: number;
  helperText?: string;
  /** The default value in form-context mode, the controlled value when `onChange` is provided. */
  value?: string | number;
  sx?: SxProps;
  inputProps?: InputProps;
  onUpdate?: (value: string) => void;
  /** Runs when the field loses focus in form-context mode, after react-hook-form's own blur handling. */
  onBlur?: () => void;
  /**
   * Controlled mode: providing this callback switches the field to standalone operation (no react-hook-form
   * context required). It receives the current value on every change.
   */
  onChange?: (value: string) => void;
  /** Extra validation run after the built-in checks in form-context mode. Return true when valid, or a message/false when invalid. */
  validate?: (value: string, formValues: FieldValues) => string | boolean;
  /** Controlled mode: error state to display. Ignored in form-context mode, which derives it from the form. */
  error?: boolean;
  /** Overrides the default `data-cy` (`${fieldName}-formInput`). */
  dataCy?: string;
}

export const FormInput: FC<FormInputProps> = ({
  fieldName,
  label,
  required,
  showRequiredIndicator,
  disabled,
  type,
  multiline,
  rows,
  minRows,
  maxRows,
  helperText,
  value,
  sx,
  inputProps,
  onUpdate,
  onChange,
  validate,
  error,
  dataCy,
}) => {
  const { t } = useTranslation();
  const formContext = useFormContext();

  const getDefaultValue = (value: string | number | undefined) => {
    if (value == undefined) {
      return "";
    } else if (type === FormValueType.DateTime) {
      // re-format from 'YYYY-MM-DDTHH:mm:ss.sssZ' to 'YYYY-MM-DDTHH:mm'.
      return (value as string).slice(0, 16);
    } else {
      return value;
    }
  };

  if (onChange) {
    return (
      <TextField
        required={required || showRequiredIndicator || false}
        error={error || false}
        sx={{ ...sx }}
        type={type || FormValueType.Text}
        multiline={multiline || false}
        rows={rows}
        minRows={minRows}
        maxRows={maxRows}
        helperText={helperText}
        label={t(label)}
        value={value ?? ""}
        onChange={e => onChange(e.target.value)}
        disabled={disabled || false}
        data-cy={dataCy ?? (fieldName ? fieldName + "-formInput" : undefined)}
        InputProps={{ ...inputProps }}
      />
    );
  }

  const { formState, register, setValue } = formContext;

  const hasError = getFormFieldError(fieldName, formState.errors);
  const errorMessage = getFormFieldErrorMessage(fieldName, formState.errors);

  return (
    <TextField
      required={required || showRequiredIndicator || false}
      error={hasError}
      sx={{ ...sx }}
      type={type || FormValueType.Text}
      multiline={multiline || false}
      rows={rows}
      minRows={minRows}
      maxRows={maxRows}
      helperText={hasError && errorMessage ? t(errorMessage) : helperText}
      label={t(label)}
      {...register(fieldName!, {
        required: required || false,
        valueAsNumber: type === FormValueType.Number,
        validate: (value, formValues) => {
          if (value !== "" && (type === FormValueType.Date || type === FormValueType.DateTime)) {
            const date = new Date(value);
            if (!(isValid(date) && date.getFullYear() > 1800 && date.getFullYear() < 3000)) {
              return false;
            }
          }
          return validate ? validate(value, formValues) : true;
        },
        onChange: e => {
          setValue(fieldName!, e.target.value, { shouldValidate: true });
          if (onUpdate) {
            onUpdate(e.target.value);
          }
        },
      })}
      defaultValue={getDefaultValue(value)}
      disabled={disabled || false}
      data-cy={dataCy ?? fieldName + "-formInput"}
      InputProps={{ ...inputProps }}
    />
  );
};
