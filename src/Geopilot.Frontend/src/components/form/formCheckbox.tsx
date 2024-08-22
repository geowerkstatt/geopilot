import { Checkbox, FormControlLabel, SxProps } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useFormContext } from "react-hook-form";
import { FC, ReactNode } from "react";

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
  const { register } = useFormContext();

  return (
    <FormControlLabel
      sx={{ ...sx }}
      control={
        <Checkbox
          data-cy={fieldName + "-formCheckbox"}
          {...register(fieldName, validation)}
          disabled={disabled || false}
          defaultChecked={checked || false}
        />
      }
      label={typeof label === "string" ? t(label) : label}
    />
  );
};
