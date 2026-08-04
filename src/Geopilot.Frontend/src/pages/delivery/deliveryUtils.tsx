import { LocalizedText, ProcessingJobResponse, StepResult, StepState } from "../../api/apiInterfaces";

const APP_HEADER_HEIGHT = 60;
export const STEPPER_HEIGHT = 58;

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

// Merges the condition messages of every delivery-restricting step into one localized reason, joining
// per language with ", " (mirroring the backend merge of multiple conditions within a single step).
// Returns undefined when no step restricts delivery.
export function getDeliveryRestrictionReason(steps: StepResult[]): LocalizedText | undefined {
  const messages = steps
    .filter(step => step.state === StepState.DeliveryRestriction)
    .map(step => step.conditionMessage)
    .filter((message): message is LocalizedText => message != null);

  if (messages.length === 0) return undefined;

  const merged: LocalizedText = {};
  for (const key of new Set(messages.flatMap(message => Object.keys(message)))) {
    merged[key] = messages
      .map(message => message[key])
      .filter(Boolean)
      .join(", ");
  }

  return merged;
}
