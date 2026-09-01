import { ChangeEvent, FC } from "react";
import { Controller, useFormContext } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { FormHelperText, Stack, SxProps, TextField } from "@mui/material";
import { Coordinate } from "../../api/apiInterfaces.ts";
import { FormContainer, FormValueType, getFormFieldError, getFormFieldErrorMessage } from "./form";

const isSet = (value?: number): value is number => value !== undefined && !isNaN(value);

/**
 * An extent only describes a rectangle when its bottom left corner lies strictly below and to the left of its
 * upper right corner. The API rejects anything else, so the form must not offer it either.
 *
 * @returns The axes whose bottom left value is at or beyond the upper right one. An incomplete extent has none,
 * because there is nothing to compare yet.
 */
const getInvertedAxes = (coordinates: Coordinate[] | undefined): { longitude: boolean; latitude: boolean } => {
  const [bottomLeft, upperRight] = coordinates ?? [];
  const noAxis = { longitude: false, latitude: false };

  if (!bottomLeft || !upperRight) {
    return noAxis;
  }
  if (!isSet(bottomLeft.x) || !isSet(bottomLeft.y) || !isSet(upperRight.x) || !isSet(upperRight.y)) {
    return noAxis;
  }

  return { longitude: bottomLeft.x >= upperRight.x, latitude: bottomLeft.y >= upperRight.y };
};

export interface FormExtentProps {
  /** Required in form-context (react-hook-form) mode; optional in controlled mode. */
  fieldName?: string;
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
  /** Controlled mode: translation key of the message shown below the fields. Ignored in form-context mode. */
  errorMessage?: string;
}

export const FormExtent: FC<FormExtentProps> = ({
  fieldName,
  required,
  disabled,
  value,
  sx,
  onChange,
  error,
  errorMessage,
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
    message?: string,
  ) => {
    const inverted = getInvertedAxes(coords);
    const isOrderError = inverted.longitude || inverted.latitude;
    const marksWholeExtent = showError && !isOrderError;
    const longitudeError = marksWholeExtent || (showError && inverted.longitude);
    const latitudeError = marksWholeExtent || (showError && inverted.latitude);

    return (
      <Stack sx={{ width: "100%" }} spacing={1}>
        <Stack>
          <FormContainer>
            <TextField
              label={t("bottomLeft") + " - " + t("longitude")}
              required={required ?? false}
              error={longitudeError}
              value={coords?.[0]?.x ?? ""}
              disabled={disabled ?? false}
              type={FormValueType.Number}
              sx={{ ...sx }}
              onChange={e => onFieldChange(0, "x", e)}
              data-cy="extent-bottom-left-longitude-formInput"
            />
            <TextField
              label={t("bottomLeft") + " - " + t("latitude")}
              required={required ?? false}
              error={latitudeError}
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
              required={required ?? false}
              error={longitudeError}
              value={coords?.[1]?.x ?? ""}
              disabled={disabled ?? false}
              type={FormValueType.Number}
              sx={{ ...sx }}
              onChange={e => onFieldChange(1, "x", e)}
              data-cy="extent-upper-right-longitude-formInput"
            />
            <TextField
              label={t("upperRight") + " - " + t("latitude")}
              required={required ?? false}
              error={latitudeError}
              value={coords?.[1]?.y ?? ""}
              disabled={disabled ?? false}
              type={FormValueType.Number}
              sx={{ ...sx }}
              onChange={e => onFieldChange(1, "y", e)}
              data-cy="extent-upper-right-latitude-formInput"
            />
          </FormContainer>
        </Stack>
        {showError && message && (
          <FormHelperText error sx={{ marginLeft: "14px" }} data-cy="extent-formHelperText">
            {t(message)}
          </FormHelperText>
        )}
      </Stack>
    );
  };

  if (onChange) {
    return renderFields(
      value,
      (index, key, e) => onChange(updateCoordinate(value, index, key, e)),
      error ?? false,
      errorMessage,
    );
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
          const allNull = value?.every(coord => !isSet(coord.x) && !isSet(coord.y));
          const noneNull = value?.every(coord => isSet(coord.x) && isSet(coord.y));

          if (allNull) {
            return !required;
          }

          if (!noneNull) {
            return false;
          }

          const inverted = getInvertedAxes(value);
          return inverted.longitude || inverted.latitude ? "spatialExtentInvalidOrder" : true;
        },
      }}
      render={({ field, formState }) =>
        renderFields(
          field.value,
          (index, key, e) =>
            setValue(fieldName!, updateCoordinate(field.value, index, key, e), {
              shouldValidate: true,
              shouldDirty: true,
            }),
          getFormFieldError(fieldName, formState.errors),
          getFormFieldErrorMessage(fieldName, formState.errors),
        )
      }
    />
  );
};
