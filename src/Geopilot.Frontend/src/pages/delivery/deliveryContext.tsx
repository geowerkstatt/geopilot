import { DeliveryContextInterface, DeliveryStep, DeliverySubmitData } from "./deliveryInterfaces.tsx";
import { createContext, FC, PropsWithChildren, useState } from "react";
import { useApi } from "../../api";
import { ApiError, ValidationResponse, ValidationStatus } from "../../api/apiInterfaces.ts";
import { DeliveryUpload } from "./deliveryUpload.tsx";
import { DeliveryValidation } from "./deliveryValidation.tsx";
import { DeliverySubmit } from "./deliverySubmit.tsx";
import { useGeopilotAuth } from "../../auth";
import { DeliveryCompleted } from "./deliveryCompleted.tsx";

export const DeliveryContext = createContext<DeliveryContextInterface>({
  steps: [],
  activeStep: 0,
  selectedFile: undefined,
  setSelectedFile: () => {},
  jobId: undefined,
  validationResults: undefined,
  isLoading: false,
  error: undefined,
  uploadFile: () => {},
  validateFile: () => {},
  submitDelivery: () => {},
  resetDelivery: () => {},
});

export const DeliveryProvider: FC<PropsWithChildren> = ({ children }) => {
  const [activeStep, setActiveStep] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string>();
  const [selectedFile, setSelectedFile] = useState<File>();
  const [validationResponse, setValidationResponse] = useState<ValidationResponse>();
  const { fetchApi } = useApi();
  const { enabled } = useGeopilotAuth();
  const controller = new AbortController();

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

  const continueToNextStep = () => {
    if (activeStep < steps.length - 1) {
      setActiveStep(activeStep + 1);
    }
  };

  const uploadFile = () => {
    if (selectedFile) {
      setIsLoading(true);
      const formData = new FormData();
      formData.append("file", selectedFile, selectedFile.name);
      fetchApi<ValidationResponse>("/api/v1/validation", {
        method: "POST",
        body: formData,
        signal: controller.signal,
      })
        .then(response => {
          setValidationResponse(response);
          continueToNextStep();
        })
        .catch((error: ApiError) => {
          setError(error.message);
        })
        .finally(() => setIsLoading(false));
    }
  };

  const validateFile = () => {
    if (validationResponse) {
      setIsLoading(true);

      const getStatus = () => {
        fetchApi<ValidationResponse>(`/api/v1/validation/${validationResponse.jobId}`, {
          method: "GET",
          signal: controller.signal,
        })
          .then(response => {
            setValidationResponse(response);
            if (response.status === ValidationStatus.Processing) {
              setTimeout(getStatus, 2000);
            } else {
              setIsLoading(false);
              switch (response.status) {
                case ValidationStatus.Completed:
                  continueToNextStep();
                  break;
                case ValidationStatus.CompletedWithErrors:
                  setError("CompletedWithErrors");
                  break;
                case ValidationStatus.Failed:
                  setError("validationFailed");
                  break;
              }
            }
          })
          .catch((error: ApiError) => {
            setError(error.message);
          });
      };

      getStatus();
    }
  };
  const submitDelivery = (data: DeliverySubmitData) => {
    setIsLoading(true);
    fetchApi<ValidationResponse>("/api/v1/delivery", {
      method: "POST",
      body: JSON.stringify({
        JobId: validationResponse?.jobId,
        MandateId: data.mandate,
        PartialDelivery: data.isPartial,
        PrecursorDeliveryId: data.predecessor,
        Comment: data.comment,
      }),
      signal: controller.signal,
    })
      .catch((error: ApiError) => {
        setError(error.message);
      })
      .finally(() => setIsLoading(false));
  };

  const resetDelivery = () => {
    controller.abort();
    setIsLoading(false);
    setSelectedFile(undefined);
    setError(undefined);
    setValidationResponse(undefined);
    setActiveStep(0);
  };

  return (
    <DeliveryContext.Provider
      value={{
        steps,
        activeStep,
        selectedFile,
        setSelectedFile,
        jobId: validationResponse?.jobId,
        validationResults: validationResponse?.validatorResults,
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
