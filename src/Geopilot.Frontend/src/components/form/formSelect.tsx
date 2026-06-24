import { FC } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { MenuItem, SxProps, TextField } from "@mui/material";
import { getFormFieldError } from "./form";

export interface FormSelectProps {
  fieldName: string;
  label: string;
  required?: boolean;
  disabled?: boolean;
  selected?: number | string;
  values?: FormSelectValue[];
  sx?: SxProps;
  onUpdate?: (value: number) => void;
  validate?: (value: number | string) => boolean | string;
}

export interface FormSelectValue {
  key: number;
  value?: number | string;
  name: string;
  hidden?: boolean;
}

interface FormSelectMenuItem {
  key: number;
  value?: number | string;
  label: string;
  italic?: boolean;
  hidden?: boolean;
}

export const FormSelect: FC<FormSelectProps> = ({
  fieldName,
  label,
  required,
  disabled,
  selected,
  values,
  sx,
  onUpdate,
  validate,
}) => {
  const { t } = useTranslation();
  const { control } = useFormContext();

  const menuItems: FormSelectMenuItem[] = [];
  if (!required) {
    menuItems.push({ key: -1, value: "", label: t("clear"), italic: true });
  }

  if (values) {
    values.forEach(value => {
      menuItems.push({
        key: value.key,
        value: value.value ?? value.key,
        label: value.name,
        hidden: value.hidden,
      });
    });
  }

  return (
    <Controller
      name={fieldName}
      control={control}
      defaultValue={selected ?? ""}
      rules={{
        required: required ?? false,
        validate,
        onChange: e => {
          if (onUpdate) {
            onUpdate(e.target.value);
          }
        },
      }}
      render={({ field, formState }) => (
        <TextField
          select
          required={required ?? false}
          error={getFormFieldError(fieldName, formState.errors)}
          helperText={
            formState.errors[fieldName]?.message ? (formState.errors[fieldName]?.message as string) : undefined
          }
          sx={{ ...sx }}
          label={t(label)}
          name={field.name}
          onChange={field.onChange}
          onBlur={field.onBlur}
          inputRef={field.ref}
          value={field.value ?? ""}
          disabled={disabled ?? false}
          data-cy={fieldName + "-formSelect"}
          InputLabelProps={{ shrink: true }}>
          {menuItems.map(item => (
            // Hidden items stay in the tree so the select can still render their label as the current value,
            // but are removed from the open dropdown so they cannot be picked.
            <MenuItem key={item.key} value={item.value} sx={item.hidden ? { display: "none" } : undefined}>
              {item.italic ? <em>{item.label}</em> : item.label}
            </MenuItem>
          ))}
        </TextField>
      )}
    />
  );
};
