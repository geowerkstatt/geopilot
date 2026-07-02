import { FC } from "react";
import { useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { InputProps, SxProps, TextField } from "@mui/material";
import { isValid } from "date-fns";
import { FormValueType, getFormFieldError } from "./form";

export interface FormInputProps {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode, where it only feeds `data-cy`. */
  fieldName?: string;
  label: string;
  required?: boolean;
  disabled?: boolean;
  type?: FormValueType;
  multiline?: boolean;
  rows?: number;
  /** The default value in form-context mode, the controlled value when `onChange` is provided. */
  value?: string | number;
  sx?: SxProps;
  inputProps?: InputProps;
  onUpdate?: (value: string) => void;
  /**
   * Controlled mode: providing this callback switches the field to standalone operation (no react-hook-form
   * context required). It receives the current value on every change.
   */
  onChange?: (value: string) => void;
  /** Controlled mode: error state to display. Ignored in form-context mode, which derives it from the form. */
  error?: boolean;
}

export const FormInput: FC<FormInputProps> = ({
  fieldName,
  label,
  required,
  disabled,
  type,
  multiline,
  rows,
  value,
  sx,
  inputProps,
  onUpdate,
  onChange,
  error,
}) => {
  const { t } = useTranslation();
  // Returns null when rendered without a FormProvider; only consumed in form-context mode.
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
        required={required || false}
        error={error || false}
        sx={{ ...sx }}
        type={type || FormValueType.Text}
        multiline={multiline || false}
        rows={rows}
        label={t(label)}
        value={value ?? ""}
        onChange={e => onChange(e.target.value)}
        disabled={disabled || false}
        data-cy={fieldName ? fieldName + "-formInput" : undefined}
        InputLabelProps={{ shrink: true }}
        InputProps={{ ...inputProps }}
      />
    );
  }

  const { formState, register, setValue } = formContext;

  return (
    <TextField
      required={required || false}
      error={getFormFieldError(fieldName, formState.errors)}
      sx={{ ...sx }}
      type={type || FormValueType.Text}
      multiline={multiline || false}
      rows={rows}
      label={t(label)}
      {...register(fieldName!, {
        required: required || false,
        valueAsNumber: type === FormValueType.Number,
        validate: value => {
          if (value === "") {
            return true;
          }
          if (type === FormValueType.Date || type === FormValueType.DateTime) {
            const date = new Date(value);
            return isValid(date) && date.getFullYear() > 1800 && date.getFullYear() < 3000;
          }
          return true;
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
      data-cy={fieldName + "-formInput"}
      InputLabelProps={{ shrink: true }}
      InputProps={{ ...inputProps }}
    />
  );
};
