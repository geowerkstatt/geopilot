import { TFunction } from "i18next";
import {
  InputKind,
  PipelineRunInputResponse,
  PipelineRunResponse,
  ProcessingState,
  StepState,
} from "../../../api/generated";
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

/**
 * The state of the preflight of a run, which the protocol keeps on the run rather than as a step: failed when the
 * preflight failed, finished once a step started or the run ended, and running until then.
 */
export const preflightStepState = (
  run: Pick<PipelineRunResponse, "outcome" | "steps" | "terminalState">,
): StepState => {
  if (run.outcome.preflightFailed) {
    return StepState.Error;
  }
  return run.terminalState || run.steps.some(step => step.state !== StepState.Pending)
    ? StepState.Success
    : StepState.Running;
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
const stepStateKey: Record<StepState, TranslationKey> = {
  [StepState.Pending]: "stepStatePending",
  [StepState.Running]: "stepStateRunning",
  [StepState.Skipped]: "stepStateSkipped",
  [StepState.Success]: "stepStateFinished",
  [StepState.Error]: "stepStateFailed",
  [StepState.Cancelled]: "stepStateCancelled",
  [StepState.Warning]: "stepStateWarning",
  [StepState.DeliveryRestriction]: "stepStateDeliveryRestriction",
};

/** The name of the state of a step, such as "Finished". */
export const formatStepState = (t: TFunction, state: StepState): string => t(stepStateKey[state]);

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
