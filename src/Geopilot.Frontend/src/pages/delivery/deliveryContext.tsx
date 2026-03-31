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
  StartJobRequest,
  UploadSettings,
  ValidationResponse,
  ValidationStatus,
} from "../../api/apiInterfaces.ts";
import { DeliveryUpload } from "./deliveryUpload.tsx";
import { DeliveryValidation } from "./validation/deliveryValidation.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { useGeopilotAuth } from "../../auth";
import { DeliveryCompleted } from "./deliveryCompleted.tsx";
import useFetch from "../../hooks/useFetch.ts";
import useCloudUpload from "../../hooks/useCloudUpload.ts";

export const DeliveryContext = createContext<DeliveryContextInterface>({
  steps: new Map<DeliveryStepEnum, DeliveryStep>(),
  activeStep: 0,
  isActiveStep: () => false,
  setStepError: () => {},
  selectedFiles: [],
  addFiles: () => {},
  removeFile: () => {},
  fileUploadStatus: new Map(),
  selectedMandate: undefined,
  setSelectedMandate: () => {},
  jobId: undefined,
  uploadSettings: undefined,
  validationResponse: undefined,
  isLoading: false,
  isValidating: false,
  uploadFile: () => {},
  cancelUpload: () => {},
  validateFile: () => {},
  submitDelivery: () => {},
  resetDelivery: () => {},
});

// Gets the current steps while reusing previous steps if possible to keep their state (e.g. errors)
const getSteps = (previousSteps: Map<DeliveryStepEnum, DeliveryStep>, showDelivery: boolean) => {
  const newSteps: Map<DeliveryStepEnum, DeliveryStep> = new Map();
  newSteps.set(
    DeliveryStepEnum.Upload,
    previousSteps.get(DeliveryStepEnum.Upload) ?? { label: "upload", content: <DeliveryUpload /> },
  );

  newSteps.set(
    DeliveryStepEnum.Validate,
    previousSteps.get(DeliveryStepEnum.Validate) ?? {
      label: "validate",
      keepOpen: true,
      content: <DeliveryValidation />,
    },
  );

  if (showDelivery) {
    newSteps.set(
      DeliveryStepEnum.Submit,
      previousSteps.get(DeliveryStepEnum.Submit) ?? { label: "deliver", content: <DeliverySubmit /> },
    );
  }

  newSteps.set(
    DeliveryStepEnum.Done,
    previousSteps.get(DeliveryStepEnum.Done) ?? { label: "done", content: <DeliveryCompleted /> },
  );

  return newSteps;
};

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [isValidating, setIsValidating] = useState(false);
  const [validationStarted, setValidationStarted] = useState(false);
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [fileUploadStatus, setFileUploadStatus] = useState<Map<string, FileUploadStatus>>(new Map());
  const [selectedMandate, setSelectedMandate] = useState<Mandate>();
  const [jobId, setJobId] = useState<string>();
  const [validationResponse, setValidationResponse] = useState<ValidationResponse>();
  const [uploadSettings, setUploadSettings] = useState<UploadSettings>();
  const [abortControllers, setAbortControllers] = useState<AbortController[]>([]);
  const { fetchApi } = useFetch();
  const { cloudUpload } = useCloudUpload();
  const { user } = useGeopilotAuth();
  const prevUserIdRef = useRef<number | undefined>(user?.id);
  const [steps, setSteps] = useState<Map<DeliveryStepEnum, DeliveryStep>>(getSteps(new Map(), false));

  const deliveryStepErrors: Record<DeliveryStepEnum, DeliveryStepError[]> = useMemo(
    () => ({
      [DeliveryStepEnum.Upload]: [
        { status: 400, errorKey: "validationErrorFileMalformed" },
        { status: 413, errorKey: "validationErrorFileTooLarge" },
        { status: 500, errorKey: "validationErrorUnexpected" },
      ],
      [DeliveryStepEnum.Validate]: [
        { status: 400, errorKey: "validationErrorRequestMalformed" },
        { status: 404, errorKey: "validationErrorCannotFind" },
        { status: 500, errorKey: "validationErrorUnexpected" },
      ],
      [DeliveryStepEnum.Submit]: [
        { status: 400, errorKey: "deliveryErrorMalformedRequest" },
        { status: 401, errorKey: "deliveryErrorUnauthorized" },
        { status: 404, errorKey: "deliveryErrorNoValidationFound" },
        { status: 500, errorKey: "deliveryErrorUnexpected" },
      ],
      [DeliveryStepEnum.Done]: [],
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
  }, [activeStep, steps]);

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
    setJobId(id);
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      const step = newSteps.get(DeliveryStepEnum.Upload);
      if (step) {
        step.labelAddition = selectedFiles.map(f => f.name).join(", ");
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

    if (uploadSettings?.enabled) {
      cloudUpload(selectedFiles, abortController.signal)
        .then(onUploadComplete)
        .catch((error: ApiError) => {
          if (abortController.signal.aborted) return;
          handleApiError(error, DeliveryStepEnum.Upload);
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
    } else {
      const file = selectedFiles[0];
      const formData = new FormData();
      formData.append("file", file, file.name);
      fetchApi<ValidationResponse>("/api/v1/validation", {
        method: "POST",
        body: formData,
        signal: abortController.signal,
      })
        .then(response => {
          setValidationResponse(response);
          setFileStatus(file.name, { state: "completed" });
          onUploadComplete(response.jobId);
        })
        .catch((error: ApiError) => {
          if (abortController.signal.aborted) return;
          handleApiError(error, DeliveryStepEnum.Upload);
          setFileStatus(file.name, { state: "error", error: error.message });
        })
        .finally(() => {
          if (abortController.signal.aborted) return;
          setIsLoading(false);
        });
    }
  };

  const cancelUpload = useCallback(() => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
    setStepError(DeliveryStepEnum.Upload, undefined);
    setFileUploadStatus(prev => {
      const next = new Map(prev);
      next.forEach((_, key) => {
        next.set(key, { state: "neutral" });
      });
      return next;
    });
  }, [abortControllers, setStepError]);

  const pollValidationStatusUntilFinished = (jobId: string, abortController: AbortController) => {
    fetchApi<ValidationResponse>(`/api/v1/validation/${jobId}`, {
      method: "GET",
      signal: abortController.signal,
    })
      .then(response => {
        setValidationResponse(response);
        if (response.status === ValidationStatus.Processing || response.status === ValidationStatus.VerifyingUpload) {
          setTimeout(() => pollValidationStatusUntilFinished(jobId, abortController), 2000);
        } else {
          setIsValidating(false);

          if (response.status === ValidationStatus.Completed) {
            continueToNextStep();
          } else {
            setStepError(DeliveryStepEnum.Validate, response.status);
          }
        }
      })
      .catch((error: ApiError) => {
        handleApiError(error, DeliveryStepEnum.Validate);
      });
  };

  const validateFile = (startJobRequest: StartJobRequest) => {
    if (!jobId || isLoading) return;
    if (!uploadSettings?.enabled && validationResponse?.status !== ValidationStatus.Ready) return;

    setIsLoading(true);
    setValidationStarted(true);

    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);

    fetchApi<ValidationResponse>(`/api/v1/validation/${jobId}`, {
      method: "PATCH",
      body: JSON.stringify(startJobRequest),
      signal: abortController.signal,
    })
      .then(response => {
        setValidationResponse(response);
        setIsLoading(false);
        setIsValidating(true);
        pollValidationStatusUntilFinished(jobId, abortController);
      })
      .catch((error: ApiError) => {
        setIsLoading(false);
        handleApiError(error, DeliveryStepEnum.Validate);
      });
  };

  const submitDelivery = (data: DeliverySubmitData) => {
    setIsLoading(true);
    if (steps.get(DeliveryStepEnum.Submit)?.error) {
      setStepError(DeliveryStepEnum.Submit, undefined);
    }
    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
    fetchApi<ValidationResponse>("/api/v1/delivery", {
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
        handleApiError(error, DeliveryStepEnum.Submit);
      })
      .finally(() => setIsLoading(false));
  };

  const resetDelivery = useCallback(() => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
    setIsValidating(false);
    setValidationStarted(false);
    setSelectedFiles([]);
    setFileUploadStatus(new Map());
    setSelectedMandate(undefined);
    setJobId(undefined);
    setValidationResponse(undefined);
    setActiveStep(0);
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      newSteps.forEach(step => {
        step.labelAddition = undefined;
        step.error = undefined;
      });
      return newSteps;
    });
  }, [abortControllers]);

  // Reset delivery when user changes AFTER the validation was already started
  useEffect(() => {
    if (validationStarted && user?.id !== prevUserIdRef.current) {
      resetDelivery();
    }
    prevUserIdRef.current = user?.id;
  }, [user?.id, resetDelivery, validationStarted]);

  return (
    <DeliveryContext.Provider
      value={{
        steps,
        activeStep,
        isActiveStep,
        setStepError,
        selectedFiles,
        addFiles,
        removeFile,
        fileUploadStatus,
        selectedMandate,
        setSelectedMandate,
        jobId,
        uploadSettings,
        validationResponse,
        isLoading,
        isValidating,
        uploadFile,
        cancelUpload,
        validateFile,
        submitDelivery,
        resetDelivery,
      }}>
      {children}
    </DeliveryContext.Provider>
  );
};
