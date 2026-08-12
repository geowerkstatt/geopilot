import { FC, ReactNode } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { Box, Checkbox, FormControlLabel, SxProps } from "@mui/material";
import { formControlLabelClasses } from "@mui/material/FormControlLabel";
import { px2rem } from "../../appTheme.ts";
import { OverflowTooltipLabel } from "./overflowTooltipLabel";

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
  /** Overrides the default `data-cy` (`${fieldName}-formCheckbox`). */
  dataCy?: string;
  /** Checkbox size, matching MUI. Defaults to "medium"; "small" also reduces the label font size. */
  size?: "small" | "medium";
  /**
   * Truncate the label to a single line with a trailing ellipsis and reveal the full text in a tooltip on
   * hover, but only when it is actually cut off. Meant for tight layouts; do not use with multi-line labels.
   */
  truncateLabel?: boolean;
  /** Wrap the label text to multiple lines if needed. Defaults to false (single line). */
  wrapLabel?: boolean;
}

export const FormCheckbox: FC<FormCheckboxProps> = ({
  fieldName,
  label,
  checked,
  disabled,
  sx,
  validation,
  onChange,
  dataCy,
  size = "medium",
  truncateLabel,
  wrapLabel,
}) => {
  const { t } = useTranslation();
  // Returns null when rendered without a FormProvider; only consumed in form-context mode.
  const formContext = useFormContext();

  const resolvedLabel = typeof label === "string" ? t(label) : label;

  return (
    <FormControlLabel
      sx={{
        [`& .${formControlLabelClasses.label}`]: {
          opacity: 1,
          ...(size === "small" && { fontSize: px2rem(14) }),
          ...(truncateLabel && { minWidth: 0 }),
        },
        ...sx,
      }}
      disabled={disabled || undefined} // passing undefined instead of false to prevent marking the form as dirty
      control={
        onChange ? (
          <Checkbox
            data-cy={dataCy ?? (fieldName ? fieldName + "-formCheckbox" : undefined)}
            disabled={disabled || false}
            checked={checked}
            size={size}
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
                data-cy={dataCy ?? fieldName + "-formCheckbox"}
                disabled={disabled || false}
                checked={field.value}
                size={size}
                onChange={e => field.onChange(e.target.checked)}
              />
            )}
          />
        )
      }
      label={
        truncateLabel ? (
          <OverflowTooltipLabel>{resolvedLabel}</OverflowTooltipLabel>
        ) : wrapLabel ? (
          <Box component="span" sx={{ wordBreak: "break-word", hyphens: "auto", py: 0.5, display: "inline-block" }}>
            {resolvedLabel}
          </Box>
        ) : (
          resolvedLabel
        )
      }
    />
  );
};
