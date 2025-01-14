import { useTranslation } from "react-i18next";
import { Controller, useFormContext } from "react-hook-form";
import { FormContainer, FormValueType, getFormFieldError } from "./form";
import { FC } from "react";
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
  const { control, setValue } = useFormContext();

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
                value={field.value?.[0]?.x}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => {
                  setValue(
                    fieldName,
                    [
                      { x: parseFloat(e.target.value), y: field.value?.[0]?.y },
                      { x: field.value?.[1]?.x, y: field.value?.[1]?.y },
                    ],
                    { shouldValidate: true },
                  );
                }}
              />
              <TextField
                label={t("bottomLeft") + " - " + t("latitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[0]?.y}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => {
                  setValue(
                    fieldName,
                    [
                      { x: field.value?.[0]?.x, y: parseFloat(e.target.value) },
                      { x: field.value?.[1]?.x, y: field.value?.[1]?.y },
                    ],
                    { shouldValidate: true },
                  );
                }}
              />
            </FormContainer>
            <FormContainer>
              <TextField
                label={t("upperRight") + " - " + t("longitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[1]?.x}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => {
                  setValue(
                    fieldName,
                    [
                      { x: field.value?.[0]?.x, y: field.value?.[0]?.y },
                      { x: parseFloat(e.target.value), y: field.value?.[1]?.y },
                    ],
                    { shouldValidate: true },
                  );
                }}
              />
              <TextField
                label={t("upperRight") + " - " + t("latitude")}
                error={getFormFieldError(fieldName, formState.errors)}
                value={field.value?.[1]?.y}
                disabled={disabled ?? false}
                type={FormValueType.Number}
                sx={{ ...sx }}
                onChange={e => {
                  setValue(
                    fieldName,
                    [
                      { x: field.value?.[0]?.x, y: field.value?.[0]?.y },
                      { x: field.value?.[1]?.x, y: parseFloat(e.target.value) },
                    ],
                    { shouldValidate: true },
                  );
                }}
              />
            </FormContainer>
          </FlexBox>
        </Stack>
      )}
    />
  );
};
