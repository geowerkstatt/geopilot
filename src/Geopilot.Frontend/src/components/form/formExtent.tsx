import { ChangeEvent, FC } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { FormLabel, Stack, SxProps, TextField } from "@mui/material";
import { Coordinate } from "../../api/apiInterfaces.ts";
import { FormContainer, FormValueType, getFormFieldError } from "./form";

export interface FormExtentProps {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode. */
  fieldName?: string;
  label: string;
  required?: boolean;
  disabled?: boolean;
  /** The default value in form-context mode, the controlled value when `onChange` is provided. */
  value?: Coordinate[];
  sx?: SxProps;
  /**
   * Controlled mode: providing this callback switches the field to standalone operation (no react-hook-form
   * context required). It receives the full extent on every change.
   */
  onChange?: (value: Coordinate[]) => void;
  /** Controlled mode: error state to display. Ignored in form-context mode, which derives it from the form. */
  error?: boolean;
}

export const FormExtent: FC<FormExtentProps> = ({
  fieldName,
  label,
  required,
  disabled,
  value,
  sx,
  onChange,
  error,
}) => {
  const { t } = useTranslation();
  const formContext = useFormContext();

  const updateCoordinate = (
    coords: Coordinate[] | undefined,
    index: number,
    key: "x" | "y",
    e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>,
  ): Coordinate[] => {
    const newValue = parseFloat(e.target.value);
    return (coords ?? []).map((coord, i) =>
      i === index ? { ...coord, [key]: isNaN(newValue) ? undefined : newValue } : coord,
    );
  };

  const renderFields = (
    coords: Coordinate[] | undefined,
    onFieldChange: (index: number, key: "x" | "y", e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void,
    showError: boolean,
  ) => (
    <Stack sx={{ width: "100%" }} gap={1}>
      <FormLabel error={showError}>{t(label) + (required ? " *" : "")}</FormLabel>
      <Stack>
        <FormContainer>
          <TextField
            label={t("bottomLeft") + " - " + t("longitude")}
            error={showError}
            value={coords?.[0]?.x ?? ""}
            disabled={disabled ?? false}
            type={FormValueType.Number}
            sx={{ ...sx }}
            onChange={e => onFieldChange(0, "x", e)}
            data-cy="extent-bottom-left-longitude-formInput"
          />
          <TextField
            label={t("bottomLeft") + " - " + t("latitude")}
            error={showError}
            value={coords?.[0]?.y ?? ""}
            disabled={disabled ?? false}
            type={FormValueType.Number}
            sx={{ ...sx }}
            onChange={e => onFieldChange(0, "y", e)}
            data-cy="extent-bottom-left-latitude-formInput"
          />
        </FormContainer>
        <FormContainer>
          <TextField
            label={t("upperRight") + " - " + t("longitude")}
            error={showError}
            value={coords?.[1]?.x ?? ""}
            disabled={disabled ?? false}
            type={FormValueType.Number}
            sx={{ ...sx }}
            onChange={e => onFieldChange(1, "x", e)}
            data-cy="extent-upper-right-longitude-formInput"
          />
          <TextField
            label={t("upperRight") + " - " + t("latitude")}
            error={showError}
            value={coords?.[1]?.y ?? ""}
            disabled={disabled ?? false}
            type={FormValueType.Number}
            sx={{ ...sx }}
            onChange={e => onFieldChange(1, "y", e)}
            data-cy="extent-upper-right-latitude-formInput"
          />
        </FormContainer>
      </Stack>
    </Stack>
  );

  if (onChange) {
    return renderFields(value, (index, key, e) => onChange(updateCoordinate(value, index, key, e)), error ?? false);
  }

  const { control, setValue } = formContext;

  return (
    <Controller
      name={fieldName!}
      control={control}
      defaultValue={value}
      rules={{
        required: required ?? false,
        validate: (value: Coordinate[]) => {
          const allNull = value?.every(
            coord => (coord.x === undefined || isNaN(coord.x)) && (coord.y === undefined || isNaN(coord.y)),
          );
          const noneNull = value?.every(
            coord => coord.x !== undefined && !isNaN(coord.x) && coord.y !== undefined && !isNaN(coord.y),
          );

          return noneNull || (allNull && !required);
        },
      }}
      render={({ field, formState }) =>
        renderFields(
          field.value,
          (index, key, e) =>
            setValue(fieldName!, updateCoordinate(field.value, index, key, e), { shouldValidate: true }),
          getFormFieldError(fieldName, formState.errors),
        )
      }
    />
  );
};
