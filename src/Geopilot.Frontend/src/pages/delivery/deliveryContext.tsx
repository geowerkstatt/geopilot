import {
  DeliveryContextInterface,
  DeliveryStep,
  DeliveryStepEnum,
  DeliveryStepError,
  DeliverySubmitData,
} from "./deliveryInterfaces.tsx";
import { createContext, FC, PropsWithChildren, useCallback, useEffect, useMemo, useState } from "react";
import { ApiError, StartJobRequest, ValidationResponse, ValidationStatus } from "../../api/apiInterfaces.ts";
import { DeliveryUpload } from "./deliveryUpload.tsx";
import { DeliveryValidation } from "./deliveryValidation.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { useGeopilotAuth } from "../../auth";
import { DeliveryCompleted } from "./deliveryCompleted.tsx";
import { useTranslation } from "react-i18next";
import useFetch from "../../hooks/useFetch.ts";

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

const GetDefaultSteps = () =>
  new Map([
    [DeliveryStepEnum.Upload, { label: "upload", content: <DeliveryUpload /> }],
    [
      DeliveryStepEnum.Validate,
      {
        label: "validate",
        keepOpen: true,
        content: <DeliveryValidation />,
      },
    ],
    [DeliveryStepEnum.Done, { label: "done", content: <DeliveryCompleted /> }],
  ]);

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const { t } = useTranslation();
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File>();
  const [validationResponse, setValidationResponse] = useState<ValidationResponse>();
  const [abortControllers, setAbortControllers] = useState<AbortController[]>([]);
  const { fetchApi } = useFetch();
  const { user } = useGeopilotAuth();
  const [steps, setSteps] = useState<Map<DeliveryStepEnum, DeliveryStep>>(GetDefaultSteps);

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

  useEffect(() => {
    const steps = GetDefaultSteps();
    if (user) {
      steps.set(DeliveryStepEnum.Submit, {
        label: "deliver",
        content: <DeliverySubmit />,
      });
    }
    setSteps(steps);
  }, [user]);

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

  const pollValidationStatusUntilFinished = (jobId: string, abortController: AbortController) => {
    fetchApi<ValidationResponse>(`/api/v1/validation/${jobId}`, {
      method: "GET",
      signal: abortController.signal,
    })
      .then(response => {
        setValidationResponse(response);
        if (response.status === ValidationStatus.Processing) {
          setTimeout(() => pollValidationStatusUntilFinished(jobId, abortController), 2000);
        } else {
          setIsLoading(false);

          if (response.status === ValidationStatus.Completed) {
            continueToNextStep();
          } else {
            setStepError(DeliveryStepEnum.Validate, t(response.status));
          }
        }
      })
      .catch((error: ApiError) => {
        handleApiError(error, DeliveryStepEnum.Validate);
      });
  };

  const validateFile = (jobId: string, startJobRequest: StartJobRequest) => {
    if (validationResponse?.status === ValidationStatus.Ready && !isLoading) {
      setIsLoading(true);

      const abortController = new AbortController();
      setAbortControllers(prevControllers => [...(prevControllers || []), abortController]);

      fetchApi<ValidationResponse>(`/api/v1/validation/${jobId}`, {
        method: "PATCH",
        body: JSON.stringify(startJobRequest),
        signal: abortController.signal,
      })
        .then(response => {
          setValidationResponse(response);
          pollValidationStatusUntilFinished(jobId, abortController);
        })
        .catch((error: ApiError) => {
          handleApiError(error, DeliveryStepEnum.Validate);
        });
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

  const resetDelivery = () => {
    abortControllers.forEach(controller => controller.abort());
    setAbortControllers([]);
    setIsLoading(false);
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
