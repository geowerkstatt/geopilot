import { ProcessingJobResponse, ProcessingState } from "../../api/apiInterfaces";

export function isProcessingDeliverable(job?: ProcessingJobResponse) {
  return job?.state === ProcessingState.Success && !job.deliveryRestrictionMessage;
}
