import { SyntheticEvent, useMemo } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Autocomplete, SxProps, TextField } from "@mui/material";
import { stopStepSwipePropagation } from "../../hooks/useStepSwipe";
import { getFormFieldError } from "./form";
import { OverflowChips } from "./overflowChips";

export interface FormAutocompleteProps<T> {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode, where it only feeds `data-cy`. */
  fieldName?: string;
  label: string;
  required?: boolean;
  disabled?: boolean;
  /** Selected values: the default value in form-context mode, the controlled value when `onChange` is provided. */
  selected?: T[];
  values?: T[];
  /**
   * Method that formats non-string values to a format that can be displayed in the autocomplete.
   * This method is mandatory for non-string values, the application will crash if it is not provided.
   *
   * Usage:
   * valueFormatter={(value) => ({
   *   id: value.id,
   *   primaryText: value.name,
   *   detailText: value.description
   * })}
   */
  valueFormatter?: (value: T) => FormAutocompleteValue;
  sx?: SxProps;

  /**
   * Controlled mode: providing this callback switches the field to standalone operation (no react-hook-form
   * context required). It receives the full selection on every change.
   */
  onChange?: (value: T[]) => void;

  /** Controlled mode: error state to display. Ignored in form-context mode, which derives it from the form. */
  error?: boolean;

  /** Overrides the default `data-cy` (`${fieldName}-formAutocomplete`). */
  dataCy?: string;

  /** Keep the dropdown open while selecting multiple values. Defaults to true. */
  disableCloseOnSelect?: boolean;
}

export interface FormAutocompleteValue {
  id: number;
  /**
   * Primary text displayed in both the chip and dropdown.
   */
  primaryText: string;
  /**
   * Extended text displayed only in the dropdown for additional context.
   * When provided, this will be shown in the dropdown instead of primaryText.
   * If not provided, primaryText will be used in the dropdown as well.
   */
  detailText?: string;
}

export const FormAutocomplete = <T,>({
  fieldName,
  label,
  required,
  disabled,
  selected,
  values,
  valueFormatter,
  sx,
  onChange,
  error,
  dataCy,
  disableCloseOnSelect,
}: FormAutocompleteProps<T>) => {
  const { t } = useTranslation();
  const formContext = useFormContext();

  const safeValueFormatter = useMemo(
    () =>
      (option: T): FormAutocompleteValue => {
        if (!valueFormatter) {
          throw new Error(`Missing valueFormatter for non-string option in FormAutocomplete "${fieldName}"`);
        }
        const formatted = valueFormatter(option);

        if (formatted.id === undefined || formatted.id === null) {
          throw new Error(`Missing mandatory ID property for non-strings in FormAutocomplete  "${fieldName}"`);
        }

        return formatted;
      },
    [fieldName, valueFormatter],
  );

  const toChipLabel = (option: T): string =>
    typeof option === "string" ? option : safeValueFormatter(option).primaryText;

  const renderAutocomplete = (
    value: T[],
    handleChange: (event: SyntheticEvent, value: T[]) => void,
    showError: boolean,
    helperText?: string,
  ) => (
    <Autocomplete
      sx={{
        width: "100%",
        // Keep the chips on a single row (OverflowChips collapses the rest into "+N"); no overflow clip here, or
        // the outlined fieldset's top border gets cut off and the focus border can't render all the way around.
        "& .MuiAutocomplete-inputRoot": { flexWrap: "nowrap" },
        ...sx,
      }}
      fullWidth
      size="small"
      slotProps={{ paper: stopStepSwipePropagation }}
      disableCloseOnSelect={disableCloseOnSelect ?? true}
      popupIcon={<ExpandMoreIcon />}
      multiple
      disabled={disabled ?? false}
      value={value}
      onChange={handleChange}
      renderValue={(tagValue, getItemProps) => (
        <OverflowChips value={tagValue.map(toChipLabel)} getItemProps={getItemProps} />
      )}
      renderInput={params => (
        <TextField
          {...params}
          label={t(label)}
          required={required ?? false}
          error={showError}
          helperText={helperText}
        />
      )}
      options={values || []}
      getOptionKey={(option: T) =>
        typeof option === "string" ? `${fieldName}-${option}` : `${fieldName}-${(values as T[]).indexOf(option)}`
      }
      getOptionLabel={(option: T) =>
        typeof option === "string"
          ? option
          : safeValueFormatter(option).detailText || safeValueFormatter(option).primaryText
      }
      isOptionEqualToValue={(option, value) =>
        typeof option === "string"
          ? (option as string) === (value as string)
          : safeValueFormatter(option).id === safeValueFormatter(value).id
      }
      data-cy={dataCy ?? (fieldName ? `${fieldName}-formAutocomplete` : undefined)}
    />
  );

  if (onChange) {
    return renderAutocomplete(selected ?? [], (_, newValue) => onChange(newValue), error ?? false);
  }

  const { control, setValue } = formContext;

  const handleFormChange = (_: SyntheticEvent, newValue: T[]) => {
    setValue(fieldName!, newValue, {
      shouldValidate: true,
      shouldDirty: true,
      shouldTouch: true,
    });
  };

  return (
    <Controller
      name={fieldName!}
      control={control}
      defaultValue={selected ?? []}
      rules={{
        required: required ?? false,
      }}
      render={({ field, formState }) =>
        renderAutocomplete(
          field.value,
          handleFormChange,
          getFormFieldError(fieldName, formState.errors),
          formState.errors[fieldName!]?.message ? t(formState.errors[fieldName!]?.message as string) : undefined,
        )
      }
    />
  );
};
