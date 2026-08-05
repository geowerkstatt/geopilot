import { LocalizedText, ProcessingJobResponse, StepResult, StepState } from "../../api/apiInterfaces";

const APP_HEADER_HEIGHT = 60;
export const STEPPER_HEIGHT = 62;

export const STICKY_TOP_POSITION_DEFAULT = APP_HEADER_HEIGHT + 40;
export const STICKY_TOP_POSITION_XS = APP_HEADER_HEIGHT + 8;

export const mobileTopDistance = STICKY_TOP_POSITION_DEFAULT + STEPPER_HEIGHT + 16; // top distance + stepper + spacing
export const smallTopDistance = STICKY_TOP_POSITION_XS + STEPPER_HEIGHT + 8; // small top distance + stepper + spacing

export function isProcessingDeliverable(job?: ProcessingJobResponse) {
  return job?.state === StepState.Success || job?.state === StepState.Warning;
}

export function normalizeJobState(state: StepState): StepState {
  return (state as string) === "failed" ? StepState.Error : state;
}

export function getConditionMessages(steps: StepResult[], state: StepState): LocalizedText[] {
  return steps
    .filter(step => step.state === state)
    .map(step => step.conditionMessage)
    .filter((message): message is LocalizedText => message != null);
}
