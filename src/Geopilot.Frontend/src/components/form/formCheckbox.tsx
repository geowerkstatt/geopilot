import { FC, ReactNode } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Checkbox, FormControlLabel, SxProps } from "@mui/material";
import { formControlLabelClasses } from "@mui/material/FormControlLabel";

export interface FormCheckboxProps {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode, where it only feeds `data-cy`. */
  fieldName?: string;
  label: string | ReactNode;
  /** The default value in form-context mode, the controlled value when `onChange` is provided. */
  checked: boolean;
  disabled?: boolean;
  sx?: SxProps;
  validation?: object;
  /**
   * Controlled mode: providing this callback switches the field to standalone operation (no react-hook-form
   * context required). It receives the checked state on every change.
   */
  onChange?: (checked: boolean) => void;
}

export const FormCheckbox: FC<FormCheckboxProps> = ({
  fieldName,
  label,
  checked,
  disabled,
  sx,
  validation,
  onChange,
}) => {
  const { t } = useTranslation();
  // Returns null when rendered without a FormProvider; only consumed in form-context mode.
  const formContext = useFormContext();

  return (
    <FormControlLabel
      sx={{ ...sx, [`& .${formControlLabelClasses.label}`]: { opacity: 1 } }}
      disabled={disabled || undefined} // passing undefined instead of false to prevent marking the form as dirty
      control={
        onChange ? (
          <Checkbox
            data-cy={fieldName ? fieldName + "-formCheckbox" : undefined}
            disabled={disabled || false}
            checked={checked}
            onChange={e => onChange(e.target.checked)}
          />
        ) : (
          <Controller
            name={fieldName!}
            control={formContext.control}
            defaultValue={checked}
            rules={validation}
            render={({ field }) => (
              <Checkbox
                {...field}
                data-cy={fieldName + "-formCheckbox"}
                disabled={disabled || false}
                checked={field.value}
                onChange={e => field.onChange(e.target.checked)}
              />
            )}
          />
        )
      }
      label={typeof label === "string" ? t(label) : label}
    />
  );
};
