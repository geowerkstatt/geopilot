import { ValidationResponse, ValidationStatus } from "../../api/apiInterfaces";

export function isValidationFinished(validationJob?: ValidationResponse) {
  return (
    validationJob?.status === ValidationStatus.Completed ||
    validationJob?.status === ValidationStatus.CompletedWithErrors ||
    validationJob?.status === ValidationStatus.Failed
  );
}
