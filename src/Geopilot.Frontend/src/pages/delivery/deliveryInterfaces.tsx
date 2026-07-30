import { ReactNode } from "react";
import { LocalizedText, Mandate, ProcessingJobResponse, UploadSettings } from "../../api/apiInterfaces.ts";

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

// A step's non-normal outcome: `state` selects the icon and colour, the optional `message` is the subtitle
// (a string is an i18n key, a LocalizedText an already-localized message). A `state` without a `message`
// renders the state without a subtitle (e.g. the processing node red without text).
export type DeliveryStepStatus = "error" | "warning" | "skipped";

export interface DeliveryStep {
  label: string;
  labelAddition?: string;
  state?: DeliveryStepStatus;
  message?: string | LocalizedText;
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
  setStepStatus: (
    key: DeliveryStepEnum,
    state: DeliveryStepStatus | undefined,
    message?: string | LocalizedText,
  ) => void;
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
  showCompletedOrNextStep: (index: number) => boolean;
  submittedData?: DeliverySubmitData;
}
