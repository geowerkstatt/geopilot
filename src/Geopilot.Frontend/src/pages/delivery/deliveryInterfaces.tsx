import { ReactNode } from "react";
import { Mandate, ProcessingJobResponse, UploadSettings } from "../../api/apiInterfaces.ts";

export enum DeliveryStepEnum {
  Files = "files",
  Mandate = "mandate",
  Processing = "processing",
  Delivery = "delivery",
}

export interface FileUploadStatus {
  state: "neutral" | "uploading" | "completed" | "error";
  error?: string;
}

export interface DeliveryStepProps {
  completed: boolean;
}

export interface DeliveryStep {
  label: string;
  labelAddition?: string;
  error?: string;
  content: (completed: boolean) => ReactNode;
}

export interface DeliverySubmitData {
  mandate: number;
  isPartial: boolean;
  precursor: number;
  comment: string;
}

export interface DeliveryStepError {
  status: number;
  errorKey: string;
}

export interface DeliveryContextInterface {
  steps: Map<DeliveryStepEnum, DeliveryStep>;
  lastCompletedStep: number;
  activeStep: number;
  isActiveStep: (step: DeliveryStepEnum) => boolean;
  setStepError: (key: DeliveryStepEnum, error: string | undefined) => void;
  selectedFiles: File[];
  addFiles: (files: File[]) => void;
  removeFile: (file: File) => void;
  fileUploadStatus: Map<string, FileUploadStatus>;
  selectedMandate?: Mandate;
  uploadId?: string;
  jobId?: string;
  uploadSettings?: UploadSettings;
  processingResponse?: ProcessingJobResponse;
  isLoading: boolean;
  isProcessing: boolean;
  uploadFile: () => void;
  startProcessing: (mandate: Mandate) => void;
  submitDelivery: (data: DeliverySubmitData) => void;
  resetDelivery: () => void;
  continueToNextStep: () => void;
  showCompletedOrNextStep: (index: number) => void;
  submittedData?: DeliverySubmitData;
}
