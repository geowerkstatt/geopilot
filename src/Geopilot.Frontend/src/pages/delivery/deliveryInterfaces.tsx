import { ReactNode } from "react";
import { ValidationResponse } from "../../api/apiInterfaces.ts";

export enum DeliveryStepEnum {
  Upload = "upload",
  Validate = "validate",
  Submit = "submit",
  Done = "done",
}

export interface DeliveryStep {
  label: string;
  labelAddition?: string;
  error?: string;
  keepOpen?: boolean;
  content: ReactNode;
}

export interface DeliverySubmitData {
  mandate: number;
  isPartial: boolean;
  precursor: number;
  comment: string;
}

export interface DeliveryContextInterface {
  steps: Map<DeliveryStepEnum, DeliveryStep>;
  activeStep: number;
  isActiveStep: (step: DeliveryStepEnum) => boolean;
  setStepError: (key: DeliveryStepEnum, error: string | undefined) => void;
  selectedFile?: File;
  setSelectedFile: (file: File | undefined) => void;
  validationResponse?: ValidationResponse;
  isLoading: boolean;
  uploadFile: () => void;
  validateFile: () => void;
  submitDelivery: (data: DeliverySubmitData) => void;
  resetDelivery: () => void;
}
