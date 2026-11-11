import { TFunction } from "i18next";
import { InputKind, InputOrigin, PipelineRunInputResponse, ProcessingState } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";

/** The time between start and end as minutes and seconds, or undefined while there is no end. */
export const formatDuration = (startedAt?: string | null, endedAt?: string | null): string | undefined => {
  if (!startedAt || !endedAt) {
    return undefined;
  }
  const milliseconds = Math.max(0, new Date(endedAt).getTime() - new Date(startedAt).getTime());
  if (milliseconds < 1000) {
    return `${milliseconds} ms`;
  }
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes} min ${seconds % 60} s` : `${seconds} s`;
};

/** The steps that kept a run from allowing a delivery, as the list has room for them: the first and how many more. */
export const formatBlockingSteps = (names: string[]): string =>
  names.length > 1 ? `${names[0]} (+${names.length - 1})` : (names[0] ?? "");

/** A file size in bytes, kilobytes or megabytes. */
export const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const inputOriginLabelKey: Record<InputOrigin, TranslationKey> = {
  [InputOrigin.Definition]: "inputOriginDefinition",
  [InputOrigin.StepOutput]: "inputOriginStepOutput",
  [InputOrigin.Resource]: "inputOriginResource",
  [InputOrigin.Upload]: "inputOriginUpload",
  [InputOrigin.Configuration]: "inputOriginConfiguration",
};

/** Where the value of a step input came from, such as "Step", without the reference that names it. */
export const formatInputOrigin = (t: TFunction, origin: InputOrigin): string => t(inputOriginLabelKey[origin]);

/**
 * What a step input shows as its value: a value as its text, a file by its name with its size when known, and
 * undefined for a reference to a skipped step, which contributes nothing.
 */
export const formatInputValue = (input: PipelineRunInputResponse): string | undefined => {
  switch (input.kind) {
    case InputKind.Value:
      return input.value ?? "null";
    case InputKind.File:
      return typeof input.size === "number"
        ? `${input.fileName ?? ""} (${formatFileSize(input.size)})`
        : (input.fileName ?? "");
    case InputKind.Absent:
      return undefined;
  }
};

// The same names as the steps of the delivery wizard.
const stateLabelKey: Record<ProcessingState, TranslationKey> = {
  [ProcessingState.Pending]: "stepStatePending",
  [ProcessingState.Running]: "stepStateRunning",
  [ProcessingState.Success]: "stepStateFinished",
  [ProcessingState.Failed]: "stepStateFailed",
  [ProcessingState.Cancelled]: "stepStateCancelled",
  [ProcessingState.Warning]: "stepStateWarning",
  [ProcessingState.DeliveryRestriction]: "stepStateDeliveryRestriction",
};

/** The state a run is shown in: the one it ended in, running while it still runs, or none when it is unknown. */
export const shownProcessingState = (state?: ProcessingState | null, running?: boolean) =>
  state ?? (running ? ProcessingState.Running : undefined);

/** The name of the state a run is shown in, such as "Finished", or that it is unknown. */
export const formatProcessingState = (t: TFunction, state?: ProcessingState): string =>
  t(state ? stateLabelKey[state] : "processingStateUnknown");
