import { createContext } from "react";
import { DeliveryContextInterface, DeliveryStep, DeliveryStepEnum } from "./deliveryInterfaces.tsx";

export const DeliveryContext = createContext<DeliveryContextInterface>({
  steps: new Map<DeliveryStepEnum, DeliveryStep>(),
  lastCompletedStep: 0,
  activeStep: 0,
  isActiveStep: () => false,
  setStepError: () => {},
  selectedFiles: [],
  addFiles: () => {},
  removeFile: () => {},
  fileUploadStatus: new Map(),
  selectedMandate: undefined,
  uploadId: undefined,
  jobId: undefined,
  uploadSettings: undefined,
  processingResponse: undefined,
  isLoading: false,
  isProcessing: false,
  uploadFile: () => {},
  startProcessing: () => {},
  submitDelivery: () => {},
  resetDelivery: () => {},
  continueToNextStep: () => {},
  showCompletedOrNextStep: () => {},
  submittedData: undefined,
});
