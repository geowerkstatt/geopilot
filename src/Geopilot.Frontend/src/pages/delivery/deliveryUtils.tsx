import { ProcessingJobResponse, ProcessingState } from "../../api/apiInterfaces";

export function isProcessingFinished(job?: ProcessingJobResponse) {
  return (
    job?.state === ProcessingState.Success ||
    job?.state === ProcessingState.Failed ||
    job?.state === ProcessingState.Cancelled
  );
}

export function isProcessingDeliverable(job?: ProcessingJobResponse) {
  return job?.state === ProcessingState.Success && !job.deliveryRestrictionMessage;
}
