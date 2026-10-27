import { ReactNode } from "react";
import { DeliveryStepState, LocalizedText, NormalizedProcessingJobResponse } from "../../api/apiInterfaces.ts";
import { DeliveryRequest, MandateSummary, UploadSettingsResponse } from "../../api/generated";
import { TranslationKey } from "../../appInterfaces";

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
  label: TranslationKey;
  labelAddition?: string;
  state?: DeliveryStepState;
  messages?: (TranslationKey | LocalizedText)[];
  content: (completed: boolean) => ReactNode;
}

export type DeliverySubmitData = Omit<DeliveryRequest, "jobId">;

export interface DeliveryStepError {
  status: number;
  errorKey: TranslationKey;
}

export interface DeliveryContextInterface {
  steps: Map<DeliveryStepEnum, DeliveryStep>;
  lastCompletedStep: number;
  activeStep: number;
  canOpenStep: (index: number) => boolean;
  isActiveStep: (step: DeliveryStepEnum) => boolean;
  setStepStatus: (
    key: DeliveryStepEnum,
    state: DeliveryStepState | undefined,
    messages?: (TranslationKey | LocalizedText)[],
  ) => void;
  selectedFiles: File[];
  addFiles: (files: File[]) => void;
  removeFile: (file: File) => void;
  fileUploadStatus: Map<string, FileUploadStatus>;
  selectedMandate?: MandateSummary;
  uploadId?: string;
  jobId?: string;
  uploadSettings?: UploadSettingsResponse;
  processingResponse?: NormalizedProcessingJobResponse;
  isLoading: boolean;
  isProcessing: boolean;
  uploadFile: () => void;
  startProcessing: (mandate: MandateSummary) => void;
  submitDelivery: (data: DeliverySubmitData) => void;
  resetDelivery: () => void;
  continueToNextStep: () => void;
  openCompletedOrNextStep: (index: number) => boolean;
  submittedData?: DeliverySubmitData;
}
