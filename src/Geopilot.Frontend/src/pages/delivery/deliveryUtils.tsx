import { LocalizedText, ProcessingJobResponse, ProcessingState, StepResult, StepState } from "../../api/apiInterfaces";

export function isProcessingDeliverable(job?: ProcessingJobResponse) {
  return job?.state === ProcessingState.Success || job?.state === ProcessingState.Warning;
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
