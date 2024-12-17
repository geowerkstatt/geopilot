import { createContext, FC, PropsWithChildren, useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApi } from "../../api";
import { ApiError, ValidationResponse, ValidationStatus } from "../../api/apiInterfaces.ts";
import { useGeopilotAuth } from "../../auth";
import { DeliveryCompleted } from "./deliveryCompleted.tsx";
import { DeliveryContextInterface, DeliveryStep, DeliveryStepEnum, DeliverySubmitData } from "./deliveryInterfaces.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { DeliveryUpload } from "./deliveryUpload.tsx";
import { DeliveryValidation } from "./deliveryValidation.tsx";

export const DeliveryContext = createContext<DeliveryContextInterface>({
  steps: new Map<DeliveryStepEnum, DeliveryStep>(),
  activeStep: 0,
  isActiveStep: () => false,
  setStepError: () => {},
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
  const { authEnabled } = useGeopilotAuth();
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
    if (authEnabled) {
      setSteps(prevSteps => {
        const newSteps = new Map(prevSteps);

        if (!newSteps.has(DeliveryStepEnum.Submit)) {
          newSteps.set(DeliveryStepEnum.Submit, {
            label: "deliver",
            content: <DeliverySubmit />,
          });
        }
        if (!newSteps.has(DeliveryStepEnum.Done)) {
          newSteps.set(DeliveryStepEnum.Done, {
            label: "done",
            content: <DeliveryCompleted />,
          });
        }
        return newSteps;
      });
    }
  }, [authEnabled]);

  const isActiveStep = (step: DeliveryStepEnum) => {
    const stepKeys = Array.from(steps.keys());
    return activeStep === stepKeys.indexOf(step);
  };

  const setStepError = (key: DeliveryStepEnum, error: string | undefined) => {
    setSteps(prevSteps => {
      const newSteps = new Map(prevSteps);
      const step = newSteps.get(key);
      if (step) {
        step.error = error;
      }
      return newSteps;
    });
  };

  const continueToNextStep = useCallback(() => {
    setAbortControllers([]);
    if (activeStep < steps.size - 1) {
      setActiveStep(activeStep + 1);
    }
  }, [activeStep, steps]);

  const handleApiError = (error: ApiError, key: DeliveryStepEnum) => {
    if (error?.message && !error?.message?.includes("AbortError")) {
      setStepError(key, error.message);
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
            const newSteps = new Map(prevSteps);
            const step = newSteps.get(DeliveryStepEnum.Upload);
            if (step) {
              step.labelAddition = selectedFile.name;
            }
            return newSteps;
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
    if (steps.get(DeliveryStepEnum.Submit)?.error) {
      setStepError(DeliveryStepEnum.Submit, undefined);
    }
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
      .then(() => {
        continueToNextStep();
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
      const newSteps = new Map(prevSteps);
      newSteps.forEach(step => {
        step.labelAddition = undefined;
        step.error = undefined;
      });
      return newSteps;
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
          setStepError(DeliveryStepEnum.Validate, t(validationResponse.status));
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
        setStepError,
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
