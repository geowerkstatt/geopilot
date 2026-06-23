import {
  DeliveryContextInterface,
  DeliveryStep,
  DeliveryStepEnum,
  DeliveryStepError,
  DeliverySubmitData,
  FileUploadStatus,
} from "./deliveryInterfaces.tsx";
import { createContext, FC, PropsWithChildren, useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  ApiError,
  Mandate,
  ProcessingJobResponse,
  ProcessingState,
  StartJobRequest,
  UploadSettings,
} from "../../api/apiInterfaces.ts";
import { DeliveryFileUpload } from "./deliveryFileUpload.tsx";
import { DeliveryProcessing } from "./processing/deliveryProcessing.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { useGeopilotAuth } from "../../auth";
import useFetch from "../../hooks/useFetch.ts";
import useCloudUpload from "../../hooks/useCloudUpload.ts";
import { isProcessingDeliverable } from "./deliveryUtils.tsx";
import { DeliverySelectMandate } from "./deliverySelectMandate.tsx";

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

// Gets the current steps while reusing previous steps if possible to keep their state (e.g. errors)
const getSteps = (previousSteps: Map<DeliveryStepEnum, DeliveryStep>, showDelivery: boolean) => {
  const newSteps: Map<DeliveryStepEnum, DeliveryStep> = new Map();
  newSteps.set(
    DeliveryStepEnum.Files,
    previousSteps.get(DeliveryStepEnum.Files) ?? {
      label: "files",
      content: completed => <DeliveryFileUpload completed={completed} />,
    },
  );

  newSteps.set(
    DeliveryStepEnum.Mandate,
    previousSteps.get(DeliveryStepEnum.Mandate) ?? {
      label: "mandate",
      content: completed => <DeliverySelectMandate completed={completed} />,
    },
  );

  newSteps.set(
    DeliveryStepEnum.Processing,
    previousSteps.get(DeliveryStepEnum.Processing) ?? {
      label: "processing",
      content: () => <DeliveryProcessing />,
    },
  );

  if (showDelivery) {
    newSteps.set(
      DeliveryStepEnum.Delivery,
      previousSteps.get(DeliveryStepEnum.Delivery) ?? {
        label: "delivery",
        content: completed => <DeliverySubmit completed={completed} />,
      },
    );
  }

  return newSteps;
};

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const [lastCompletedStep, setLastCompletedStep] = useState(-1);
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [processingStarted, setProcessingStarted] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [fileUploadStatus, setFileUploadStatus] = useState<Map<string, FileUploadStatus>>(new Map());
  const [selectedMandate, setSelectedMandate] = useState<Mandate>();
  const [uploadId, setUploadId] = useState<string>();
  const [jobId, setJobId] = useState<string>();
  const [processingResponse, setProcessingResponse] = useState<ProcessingJobResponse>();
  const [uploadSettings, setUploadSettings] = useState<UploadSettings>();
  const [abortControllers, setAbortControllers] = useState<AbortController[]>([]);
  const { fetchApi } = useFetch();
  const { cloudUpload } = useCloudUpload();
  const { user } = useGeopilotAuth();
  const prevUserIdRef = useRef<number | undefined>(user?.id);
  const [steps, setSteps] = useState<Map<DeliveryStepEnum, DeliveryStep>>(getSteps(new Map(), false));
  const [submittedData, setSubmittedData] = useState<DeliverySubmitData>();

  const deliveryStepErrors: Record<DeliveryStepEnum, DeliveryStepError[]> = useMemo(
    () => ({
      [DeliveryStepEnum.Files]: [
        { status: 400, errorKey: "validationErrorFileMalformed" },
        { status: 413, errorKey: "validationErrorFileTooLarge" },
        { status: 500, errorKey: "validationErrorUnexpected" },
      ],
      [DeliveryStepEnum.Mandate]: [],
      [DeliveryStepEnum.Processing]: [
        { status: 400, errorKey: "validationErrorRequestMalformed" },
        { status: 404, errorKey: "validationErrorCannotFind" },
        { status: 500, errorKey: "validationErrorUnexpected" },
      ],
      [DeliveryStepEnum.Delivery]: [
        { status: 400, errorKey: "deliveryErrorMalformedRequest" },
        { status: 401, errorKey: "deliveryErrorUnauthorized" },
        { status: 404, errorKey: "deliveryErrorNoValidationFound" },
        { status: 500, errorKey: "deliveryErrorUnexpected" },
      ],
    }),
    [],
  );

  // Update steps depending on if user is logged in or not
  useEffect(() => {
    setSteps(prevSteps =>
      getSteps(prevSteps, user != null && selectedMandate != null && selectedMandate.allowDelivery),
    );
  }, [user, selectedMandate]);

  useEffect(() => {
    fetchApi<UploadSettings>("/api/v2/upload").then(setUploadSettings);
  }, [fetchApi]);

  const isActiveStep = (step: DeliveryStepEnum) => {
    const stepKeys = Array.from(steps.keys());
    return activeStep === stepKeys.indexOf(step);
  };

  const setStepError = useCallback((key: DeliveryStepEnum, error: string | undefined) => {
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      const step = newSteps.get(key);
      if (step) {
        step.error = error;
      }
      return newSteps;
    });
  }, []);

  const addFiles = useCallback((newFiles: File[]) => {
    setSelectedFiles(prev => {
      const existingNames = new Set(prev.map(f => f.name));
      const uniqueNewFiles = newFiles.filter(f => !existingNames.has(f.name));
      return [...prev, ...uniqueNewFiles];
    });
    setFileUploadStatus(prev => {
      const next = new Map(prev);
      newFiles.forEach(f => {
        if (!next.has(f.name)) {
          next.set(f.name, { state: "neutral" });
        }
      });
      return next;
    });
  }, []);

  const removeFile = useCallback((file: File) => {
    setSelectedFiles(prev => prev.filter(f => f.name !== file.name));
    setFileUploadStatus(prev => {
      const next = new Map(prev);
      next.delete(file.name);
      return next;
    });
  }, []);

  const continueToNextStep = useCallback(() => {
    setAbortControllers([]);
    if (activeStep < steps.size - 1) {
      setActiveStep(activeStep + 1);
    }
    if (activeStep < steps.size) {
      setLastCompletedStep(completed => Math.max(completed, activeStep));
    }
  }, [activeStep, steps]);

  const markStepCompleted = useCallback(() => {
    setLastCompletedStep(prev => Math.max(prev + 1, steps.size - 1));
  }, [steps]);

  const showCompletedOrNextStep = useCallback(
    (index: number) => {
      if (index >= 0 && index <= lastCompletedStep + 1) {
        setActiveStep(index);
      }
    },
    [lastCompletedStep],
  );

  const handleApiError = useCallback(
    (error: ApiError, key: DeliveryStepEnum) => {
      if (error && error.message && !error.message.includes("AbortError")) {
        setStepError(
          key,
          deliveryStepErrors[key].find(stepError => stepError.status === error.status)?.errorKey || error.message,
        );
      }
    },
    [deliveryStepErrors, setStepError],
  );

  const onUploadComplete = (id: string) => {
    setUploadId(id);
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      const step = newSteps.get(DeliveryStepEnum.Files);
      if (step) {
        step.labelAddition = selectedFiles.map(f => f.name).join("\n");
      }
      return newSteps;
    });
    continueToNextStep();
  };

  const setFileStatus = (fileName: string, status: FileUploadStatus) => {
    setFileUploadStatus(prev => {
      const next = new Map(prev);
      next.set(fileName, status);
      return next;
    });
  };

  const uploadFile = () => {
    if (selectedFiles.length === 0) return;

    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
    setIsLoading(true);

    selectedFiles.forEach(f => setFileStatus(f.name, { state: "uploading" }));

    cloudUpload(selectedFiles, abortController.signal)
      .then(onUploadComplete)
      .catch((error: ApiError) => {
        if (abortController.signal.aborted) return;
        handleApiError(error, DeliveryStepEnum.Files);
        selectedFiles.forEach(f => setFileStatus(f.name, { state: "error", error: error.message }));
      })
      .finally(() => {
        if (abortController.signal.aborted) return;
        setIsLoading(false);
        setFileUploadStatus(prev => {
          const next = new Map(prev);
          next.forEach((status, key) => {
            if (status.state === "uploading") {
              next.set(key, { state: "completed" });
            }
          });
          return next;
        });
      });
  };

  const pollProcessingStatusUntilFinished = (jobId: string, abortController: AbortController) => {
    fetchApi<ProcessingJobResponse>(`/api/v2/processing/${jobId}`, {
      method: "GET",
      signal: abortController.signal,
    })
      .then(response => {
        setProcessingResponse(response);
        if (response.state === ProcessingState.Pending || response.state === ProcessingState.Running) {
          setTimeout(() => pollProcessingStatusUntilFinished(jobId, abortController), 2000);
        } else {
          setIsProcessing(false);

          if (isProcessingDeliverable(response)) {
            markStepCompleted();
          } else if (response.state === ProcessingState.Success) {
            // Pipeline succeeded but delivery is blocked (e.g. delivery restriction matched).
            setStepError(DeliveryStepEnum.Processing, "completedWithErrors");
          } else {
            // ProcessingState.Failed or Cancelled.
            setStepError(DeliveryStepEnum.Processing, response.state);
          }
        }
      })
      .catch((error: ApiError) => {
        handleApiError(error, DeliveryStepEnum.Processing);
      });
  };

  const startProcessing = (mandate: Mandate) => {
    if (!uploadId || isLoading) return;

    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      const step = newSteps.get(DeliveryStepEnum.Mandate);
      if (step) {
        step.labelAddition = mandate.name;
      }
      return newSteps;
    });
    setSelectedMandate(mandate);
    setIsLoading(true);
    setProcessingStarted(true);
    continueToNextStep();

    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);

    const startJobRequest: StartJobRequest = {
      mandateId: mandate.id,
      uploadId,
    };

    fetchApi<ProcessingJobResponse>("/api/v2/processing", {
      method: "POST",
      body: JSON.stringify(startJobRequest),
      signal: abortController.signal,
    })
      .then(response => {
        setJobId(response.jobId);
        setProcessingResponse(response);
        setIsLoading(false);
        setIsProcessing(true);
        pollProcessingStatusUntilFinished(response.jobId, abortController);
      })
      .catch((error: ApiError) => {
        setIsLoading(false);
        handleApiError(error, DeliveryStepEnum.Processing);
      });
  };

  const submitDelivery = (data: DeliverySubmitData) => {
    setIsLoading(true);
    setSubmittedData(data);
    if (steps.get(DeliveryStepEnum.Delivery)?.error) {
      setStepError(DeliveryStepEnum.Delivery, undefined);
    }
    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
    fetchApi<ProcessingJobResponse>("/api/v1/delivery", {
      method: "POST",
      body: JSON.stringify({
        JobId: jobId,
        MandateId: data.mandate,
        PartialDelivery: data.isPartial,
        PrecursorDeliveryId: data.precursor,
        Comment: data.comment,
      }),
      signal: abortController.signal,
    })
      .then(() => {
        continueToNextStep();
      })
      .catch((error: ApiError) => {
        handleApiError(error, DeliveryStepEnum.Delivery);
      })
      .finally(() => setIsLoading(false));
  };

  const resetDelivery = useCallback(() => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
    setIsProcessing(false);
    setProcessingStarted(false);
    setSelectedFiles([]);
    setFileUploadStatus(new Map());
    setSelectedMandate(undefined);
    setUploadId(undefined);
    setJobId(undefined);
    setProcessingResponse(undefined);
    setActiveStep(0);
    setLastCompletedStep(-1);
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      newSteps.forEach(step => {
        step.labelAddition = undefined;
        step.error = undefined;
      });
      return newSteps;
    });
    setSubmittedData(undefined);
  }, [abortControllers]);

  // Reset delivery when user changes AFTER processing was already started
  useEffect(() => {
    if (processingStarted && user?.id !== prevUserIdRef.current) {
      resetDelivery();
    }
    prevUserIdRef.current = user?.id;
  }, [user?.id, resetDelivery, processingStarted]);

  return (
    <DeliveryContext.Provider
      value={{
        steps,
        lastCompletedStep,
        activeStep,
        isActiveStep,
        setStepError,
        selectedFiles,
        addFiles,
        removeFile,
        fileUploadStatus,
        selectedMandate,
        uploadId,
        jobId,
        uploadSettings,
        processingResponse,
        isLoading,
        isProcessing,
        uploadFile,
        startProcessing,
        submitDelivery,
        resetDelivery,
        continueToNextStep,
        showCompletedOrNextStep,
        submittedData,
      }}>
      {children}
    </DeliveryContext.Provider>
  );
};
