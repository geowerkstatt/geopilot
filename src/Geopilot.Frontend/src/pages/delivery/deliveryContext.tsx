import { DeliveryContextInterface, DeliveryStep, DeliveryStepEnum, DeliverySubmitData } from "./deliveryInterfaces.tsx";
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
  steps: new Map<DeliveryStepEnum, DeliveryStep>(),
  activeStep: 0,
  isActiveStep: () => false,
  selectedFile: undefined,
  setSelectedFile: () => {},
  validationResponse: undefined,
  isLoading: false,
  uploadFile: () => {},
  validateFile: () => {},
  submitDelivery: () => {},
  resetDelivery: () => {},
});

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const { t } = useTranslation();
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File>();
  const [validationResponse, setValidationResponse] = useState<ValidationResponse>();
  const [isValidating, setIsValidating] = useState<boolean>(false);
  const [abortControllers, setAbortControllers] = useState<AbortController[]>([]);
  const { fetchApi } = useApi();
  const { enabled } = useGeopilotAuth();
  const [steps, setSteps] = useState<Map<DeliveryStepEnum, DeliveryStep>>(
    new Map<DeliveryStepEnum, DeliveryStep>([
      [DeliveryStepEnum.Upload, { label: "upload", content: <DeliveryUpload /> }],
      [
        DeliveryStepEnum.Validate,
        {
          label: "validate",
          keepOpen: true,
          content: <DeliveryValidation />,
        },
      ],
    ]),
  );

  useEffect(() => {
    if (enabled) {
      setSteps(prevSteps => {
        if (!prevSteps.has(DeliveryStepEnum.Submit)) {
          prevSteps.set(DeliveryStepEnum.Submit, {
            label: "deliver",
            content: <DeliverySubmit />,
          });
        }
        if (!prevSteps.has(DeliveryStepEnum.Done)) {
          prevSteps.set(DeliveryStepEnum.Done, {
            label: "done",
            content: <DeliveryCompleted />,
          });
        }
        return prevSteps;
      });
    }
  }, [enabled]);

  const isActiveStep = (step: DeliveryStepEnum) => {
    const stepKeys = Array.from(steps.keys());
    return activeStep === stepKeys.indexOf(step);
  };

  const continueToNextStep = useCallback(() => {
    setAbortControllers([]);
    if (activeStep < steps.size - 1) {
      setActiveStep(activeStep + 1);
    }
  }, [activeStep, steps]);

  const handleApiError = (error: ApiError, key: DeliveryStepEnum) => {
    if (error?.message && !error?.message?.includes("AbortError")) {
      setSteps(prevSteps => {
        const step = prevSteps.get(key);
        if (step) {
          step.error = error.message;
        }
        return prevSteps;
      });
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
          setSteps(prevSteps => {
            const step = prevSteps.get(DeliveryStepEnum.Upload);
            if (step) {
              step.labelAddition = selectedFile.name;
            }
            return prevSteps;
          });
          continueToNextStep();
        })
        .catch((error: ApiError) => {
          handleApiError(error, DeliveryStepEnum.Upload);
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
        handleApiError(error, DeliveryStepEnum.Validate);
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
        handleApiError(error, DeliveryStepEnum.Submit);
      })
      .finally(() => setIsLoading(false));
  };

  const resetDelivery = () => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
    setIsValidating(false);
    setSelectedFile(undefined);
    setValidationResponse(undefined);
    setActiveStep(0);
    setSteps(prevSteps => {
      prevSteps.forEach(step => {
        step.labelAddition = undefined;
        step.error = undefined;
      });
      return prevSteps;
    });
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
          setSteps(prevSteps => {
            const step = prevSteps.get(DeliveryStepEnum.Validate);
            if (step) {
              step.error = t(validationResponse.status);
            }
            return prevSteps;
          });
        }
      }
    }
  }, [abortControllers.length, continueToNextStep, getValidationStatus, isValidating, t, validationResponse]);

  return (
    <DeliveryContext.Provider
      value={{
        steps,
        activeStep,
        isActiveStep,
        selectedFile,
        setSelectedFile,
        validationResponse,
        isLoading,
        uploadFile,
        validateFile,
        submitDelivery,
        resetDelivery,
      }}>
      {children}
    </DeliveryContext.Provider>
  );
};
