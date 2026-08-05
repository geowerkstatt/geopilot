import {
  LocalizedText,
  ProcessingJobResponse,
  ProcessingState,
  RawProcessingJobResponse,
  StepResult,
  StepState,
} from "../../api/apiInterfaces";

const APP_HEADER_HEIGHT = 60;
export const STEPPER_HEIGHT = 62;

export const STICKY_TOP_POSITION_DEFAULT = APP_HEADER_HEIGHT + 40;
export const STICKY_TOP_POSITION_XS = APP_HEADER_HEIGHT + 8;

export const mobileTopDistance = STICKY_TOP_POSITION_DEFAULT + STEPPER_HEIGHT + 16; // top distance + stepper + spacing
export const smallTopDistance = STICKY_TOP_POSITION_XS + STEPPER_HEIGHT + 8; // small top distance + stepper + spacing

export function isProcessingDeliverable(job?: ProcessingJobResponse) {
  return job?.state === StepState.Success || job?.state === StepState.Warning;
}

/**
 * Normalizes a raw processing job into the model used across the app: the job-level {@link ProcessingState}
 * is mapped onto the shared {@link StepState}. Only `"failed"` needs translating (to {@link StepState.Error});
 * every other job state shares its string value with a {@link StepState} member.
 */
export function normalizeProcessingJob(job: RawProcessingJobResponse): ProcessingJobResponse {
  return { ...job, state: processingStateToStepState(job.state) };
}

const processingStateToStepState = (state: ProcessingState): StepState => {
  switch (state) {
    case "pending":
      return StepState.Pending;
    case "running":
      return StepState.Running;
    case "success":
      return StepState.Success;
    case "failed":
      return StepState.Error;
    case "cancelled":
      return StepState.Cancelled;
    case "warning":
      return StepState.Warning;
    case "deliveryRestriction":
      return StepState.DeliveryRestriction;
    default: {
      const exhaustiveCheck: never = state;
      return exhaustiveCheck;
    }
  }
};

export function getConditionMessages(steps: StepResult[], state: StepState): LocalizedText[] {
  return steps
    .filter(step => step.state === state)
    .map(step => step.conditionMessage)
    .filter((message): message is LocalizedText => message != null);
}
