import { FC, ReactNode } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Checkbox, FormControlLabel, SxProps } from "@mui/material";
import { formControlLabelClasses } from "@mui/material/FormControlLabel";

export interface FormCheckboxProps {
  fieldName: string;
  label: string | ReactNode;
  checked: boolean;
  disabled?: boolean;
  sx?: SxProps;
  validation?: object;
}

export const FormCheckbox: FC<FormCheckboxProps> = ({ fieldName, label, checked, disabled, sx, validation }) => {
  const { t } = useTranslation();
  const { control } = useFormContext();

  return (
    <FormControlLabel
      sx={{ ...sx, [`& .${formControlLabelClasses.label}`]: { opacity: 1 } }}
      disabled={disabled || undefined} // passing undefined instead of false to prevent marking the form as dirty
      control={
        <Controller
          name={fieldName}
          control={control}
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
      }
      label={typeof label === "string" ? t(label) : label}
    />
  );
};
