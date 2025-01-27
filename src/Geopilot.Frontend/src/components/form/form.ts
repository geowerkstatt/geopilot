import { FieldError, FieldErrorsImpl } from "react-hook-form/dist/types/errors";
import { Merge } from "react-hook-form";
import { styled } from "@mui/system";
import { FlexBox } from "../styledComponents.ts";

export const getFormFieldError = (
  fieldName: string | undefined,
  errors: FieldError | Merge<FieldError, FieldErrorsImpl> | undefined,
) => {
  if (!fieldName || !errors) {
    return false;
  }

  const fieldNameElements = fieldName ? fieldName.split(".") : [];
  let currentElement = errors;
  for (let i = 0; i < fieldNameElements.length; i++) {
    // @ts-expect-error - we know that currentElement either has a key of fieldNameElements[i] or it doesn't,
    // which is what we're checking for
    currentElement = currentElement[fieldNameElements[i]];
    if (!currentElement) {
      break;
    }
  }
  return !!currentElement;
};

export enum FormValueType {
  Text = "text",
  Number = "number",
  Date = "date",
  DateTime = "datetime-local",
}

export const FormContainer = styled(FlexBox)(({ theme }) => ({
  [theme.breakpoints.up("md")]: {
    flexDirection: "row",
  },
}));

export const FormContainerHalfWidth = styled(FormContainer)(({ theme }) => ({
  [theme.breakpoints.up("md")]: {
    width: `calc(50% - ${theme.spacing(1)})`,
  },
}));

export { FormInput } from "./formInput";
export { FormSelect } from "./formSelect";
export { FormCheckbox } from "./formCheckbox";
export { FormAutocomplete } from "./formAutocomplete";
export { FormExtent } from "./formExtent";
