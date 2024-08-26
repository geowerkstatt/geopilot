import { DeliveryContextInterface, DeliveryStep, DeliverySubmitData } from "./deliveryInterfaces.tsx";
import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { useApi } from "../../api";
import { ApiError, ValidationResponse, ValidationStatus } from "../../api/apiInterfaces.ts";
import { DeliveryUpload } from "./deliveryUpload.tsx";
import { DeliveryValidation } from "./deliveryValidation.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { useGeopilotAuth } from "../../auth";
import { DeliveryCompleted } from "./deliveryCompleted.tsx";
import { useTranslation } from "react-i18next";

export const DeliveryContext = createContext<DeliveryContextInterface>({
  steps: [],
  activeStep: 0,
  selectedFile: undefined,
  setSelectedFile: () => {},
  validationResponse: undefined,
  isLoading: false,
  error: undefined,
  uploadFile: () => {},
  validateFile: () => {},
  submitDelivery: () => {},
  resetDelivery: () => {},
});

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const { t } = useTranslation();
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [selectedFile, setSelectedFile] = useState<File>();
  const [validationResponse, setValidationResponse] = useState<ValidationResponse>();
  const [isValidating, setIsValidating] = useState<boolean>(false);
  const [abortControllers, setAbortControllers] = useState<AbortController[]>([]);
  const { fetchApi } = useApi();
  const { enabled } = useGeopilotAuth();

  const steps: DeliveryStep[] = [
    {
      label: "upload",
      content: <DeliveryUpload />,
    },
    {
      label: "validate",
      keepOpen: true,
      content: <DeliveryValidation />,
    },
  ];

  if (enabled) {
    steps.push({
      label: "deliver",
      content: <DeliverySubmit />,
    });
    steps.push({
      label: "done",
      content: <DeliveryCompleted />,
    });
  }

  const continueToNextStep = useCallback(() => {
    setAbortControllers([]);
    if (activeStep < steps.length - 1) {
      setActiveStep(activeStep + 1);
    }
  }, [activeStep, steps]);

  const handleApiError = (error: ApiError) => {
    if (!error.message.includes("AbortError")) {
      setError(error.message);
    }
  };

  const uploadFile = () => {
    if (selectedFile) {
      const abortController = new AbortController();
      setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
      setIsLoading(true);
      const formData = new FormData();
      formData.append("file", selectedFile, selectedFile.name);
      fetchApi<ValidationResponse>("/api/v1/validation", {
        method: "POST",
        body: formData,
        signal: abortController.signal,
      })
        .then(response => {
          setValidationResponse(response);
          continueToNextStep();
        })
        .catch((error: ApiError) => {
          handleApiError(error);
        })
        .finally(() => setIsLoading(false));
    }
  };

  const getValidationStatus = useCallback(() => {
    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
    fetchApi<ValidationResponse>(`/api/v1/validation/${validationResponse?.jobId}`, {
      method: "GET",
      signal: abortController.signal,
    })
      .then(response => {
        setValidationResponse(response);
      })
      .catch((error: ApiError) => {
        handleApiError(error);
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [validationResponse?.jobId]);

  const validateFile = () => {
    if (validationResponse?.status === ValidationStatus.Processing && !isLoading) {
      setIsLoading(true);
      setIsValidating(true);
      getValidationStatus();
    }
  };

  const submitDelivery = (data: DeliverySubmitData) => {
    setIsLoading(true);
    const abortController = new AbortController();
    setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);
    fetchApi<ValidationResponse>("/api/v1/delivery", {
      method: "POST",
      body: JSON.stringify({
        JobId: validationResponse?.jobId,
        MandateId: data.mandate,
        PartialDelivery: data.isPartial,
        PrecursorDeliveryId: data.predecessor,
        Comment: data.comment,
      }),
      signal: abortController.signal,
    })
      .catch((error: ApiError) => {
        handleApiError(error);
      })
      .finally(() => setIsLoading(false));
  };

  const resetDelivery = () => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
    setIsValidating(false);
    setSelectedFile(undefined);
    setError(undefined);
    setValidationResponse(undefined);
    setActiveStep(0);
  };

  useEffect(() => {
    if (validationResponse && isValidating && abortControllers.length > 0) {
      if (validationResponse.status === ValidationStatus.Processing) {
        setTimeout(getValidationStatus, 2000);
      } else {
        setIsValidating(false);
        setIsLoading(false);
        if (validationResponse.status === ValidationStatus.Completed) {
          continueToNextStep();
        } else {
          setError(t(validationResponse.status));
        }
      }
    }
  }, [abortControllers.length, continueToNextStep, getValidationStatus, isValidating, t, validationResponse]);

  return (
    <DeliveryContext.Provider
      value={{
        steps,
        activeStep,
        selectedFile,
        setSelectedFile,
        validationResponse,
        isLoading,
        error,
        uploadFile,
        validateFile,
        submitDelivery,
        resetDelivery,
      }}>
      {children}
    </DeliveryContext.Provider>
  );
};
