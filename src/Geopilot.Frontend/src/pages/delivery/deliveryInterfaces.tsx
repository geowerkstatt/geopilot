import { ReactNode } from "react";
import { ValidationResponse } from "../../api/apiInterfaces.ts";

export interface DeliveryStep {
  label: string;
  keepOpen?: boolean;
  content: ReactNode;
}

export interface DeliverySubmitData {
  mandate: number;
  isPartial: boolean;
  predecessor: number;
  comment: string;
}

export interface DeliveryContextInterface {
  steps: DeliveryStep[];
  activeStep: number;
  selectedFile?: File;
  setSelectedFile: (file: File | undefined) => void;
  validationResponse?: ValidationResponse;
  isLoading: boolean;
  error?: string;
  uploadFile: () => void;
  validateFile: () => void;
  submitDelivery: (data: DeliverySubmitData) => void;
  resetDelivery: () => void;
}
