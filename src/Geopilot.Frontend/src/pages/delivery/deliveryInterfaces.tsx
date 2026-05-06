import { ReactNode } from "react";
import { Mandate, ProcessingJobResponse, StartJobRequest, UploadSettings } from "../../api/apiInterfaces.ts";

export enum DeliveryStepEnum {
  Upload = "upload",
  Process = "process",
  Submit = "submit",
  Done = "done",
}

export interface FileUploadStatus {
  state: "neutral" | "uploading" | "completed" | "error";
  error?: string;
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

export interface DeliveryStepError {
  status: number;
  errorKey: string;
}

export interface DeliveryContextInterface {
  steps: Map<DeliveryStepEnum, DeliveryStep>;
  activeStep: number;
  isActiveStep: (step: DeliveryStepEnum) => boolean;
  setStepError: (key: DeliveryStepEnum, error: string | undefined) => void;
  selectedFiles: File[];
  addFiles: (files: File[]) => void;
  removeFile: (file: File) => void;
  fileUploadStatus: Map<string, FileUploadStatus>;
  selectedMandate?: Mandate;
  setSelectedMandate: (mandate: Mandate | undefined) => void;
  jobId?: string;
  uploadSettings?: UploadSettings;
  processingResponse?: ProcessingJobResponse;
  isLoading: boolean;
  isProcessing: boolean;
  uploadFile: () => void;
  cancelUpload: () => void;
  startProcessing: (startJobRequest: StartJobRequest) => void;
  submitDelivery: (data: DeliverySubmitData) => void;
  resetDelivery: () => void;
}
