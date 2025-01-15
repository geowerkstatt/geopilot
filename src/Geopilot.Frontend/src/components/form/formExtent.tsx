import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { FormContainer, FormValueType, getFormFieldError } from "./form";
import { ChangeEvent, FC } from "react";
import { FormLabel, Stack, SxProps, TextField } from "@mui/material";
import { FlexBox } from "../styledComponents.ts";
import { Coordinate } from "../../api/apiInterfaces.ts";

export interface FormExtentProps {
  fieldName: string;
  label: string;
  required?: boolean;
  disabled?: boolean;
  value?: Coordinate[];
  sx?: SxProps;
}

export const FormExtent: FC<FormExtentProps> = ({ fieldName, label, required, disabled, value, sx }) => {
  const { t } = useTranslation();
  const { control, getValues, setValue } = useFormContext();

  const handleChange = (index: number, key: "x" | "y", e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const newValue = parseFloat(e.target.value);
    const existingValue = getValues(fieldName);
    setValue(
      fieldName,
      existingValue?.map((coord: Coordinate, i: number) =>
        i === index ? { ...coord, [key]: isNaN(newValue) ? undefined : newValue } : coord,
      ),
      { shouldValidate: true },
    );
  };

  return (
    <Controller
      name={fieldName}
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
      render={({ field, formState }) => (
        <Stack sx={{ width: "100%" }}>
          <FormLabel error={getFormFieldError(fieldName, formState.errors)}>
            {t(label) + (required ? " *" : "")}
          </FormLabel>
          <FlexBox>
            <FormContainer>
              <TextField
                label={t("bottomLeft") + " - " + t("longitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[0]?.x ?? ""}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => handleChange(0, "x", e)}
                data-cy="extent-bottom-left-longitude-formInput"
              />
              <TextField
                label={t("bottomLeft") + " - " + t("latitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[0]?.y ?? ""}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => handleChange(0, "y", e)}
                data-cy="extent-bottom-left-latitude-formInput"
              />
            </FormContainer>
            <FormContainer>
              <TextField
                label={t("upperRight") + " - " + t("longitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[1]?.x ?? ""}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => handleChange(1, "x", e)}
                data-cy="extent-upper-right-longitude-formInput"
              />
              <TextField
                label={t("upperRight") + " - " + t("latitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[1]?.y ?? ""}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => handleChange(1, "y", e)}
                data-cy="extent-upper-right-latitude-formInput"
              />
            </FormContainer>
          </FlexBox>
        </Stack>
      )}
    />
  );
};
