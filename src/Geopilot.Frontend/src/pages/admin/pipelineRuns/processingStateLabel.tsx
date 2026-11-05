import { FC } from "react";
import { useTranslation } from "react-i18next";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlineOutlined";
import ScheduleIcon from "@mui/icons-material/Schedule";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { CircularProgress, Stack, Tooltip } from "@mui/material";
import { PipelineRunBlockingStep, ProcessingState } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { formatBlockingSteps } from "./pipelineRunFormat.ts";

interface ProcessingStateLabelProps {
  /** The state the run ended in; without one, its outcome is unknown or it still runs. */
  state?: ProcessingState | null;
  /** Whether a run without terminal state still runs, rather than having stopped with its instance. */
  running?: boolean;
  /** The steps that kept the run from allowing a delivery, named after the state as its reason. */
  blockingSteps?: PipelineRunBlockingStep[];
  /** Name every blocking step, rather than the first and how many more. */
  allSteps?: boolean;
}

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

const stateColor: Record<ProcessingState, string> = {
  [ProcessingState.Pending]: "primary.main",
  [ProcessingState.Running]: "primary.main",
  [ProcessingState.Success]: "primary.main",
  [ProcessingState.Failed]: "error.main",
  [ProcessingState.Cancelled]: "error.main",
  [ProcessingState.Warning]: "warning.main",
  [ProcessingState.DeliveryRestriction]: "error.main",
};

// The icons take the color of the state.
const renderIcon = (state: ProcessingState) => {
  switch (state) {
    case ProcessingState.Pending:
      return <ScheduleIcon fontSize="small" />;
    case ProcessingState.Running:
      return <CircularProgress size={px2rem(16)} color="inherit" aria-hidden />;
    case ProcessingState.Success:
      return <CheckCircleOutlineIcon fontSize="small" />;
    case ProcessingState.Warning:
      return <WarningAmberIcon fontSize="small" />;
    case ProcessingState.DeliveryRestriction:
      return <BlockIcon fontSize="small" />;
    case ProcessingState.Failed:
    case ProcessingState.Cancelled:
      return <ErrorOutlineIcon fontSize="small" />;
  }
};

/** The state of a pipeline run as a colored icon with its name. */
export const ProcessingStateLabel: FC<ProcessingStateLabelProps> = ({ state, running, blockingSteps, allSteps }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const shownState = state ?? (running ? ProcessingState.Running : undefined);
  const label = t(shownState ? stateLabelKey[shownState] : "processingStateUnknown");

  // A restriction does not stop the pipeline, so several steps can keep the run from allowing a delivery.
  const steps = (blockingSteps ?? []).map(step => localized(step.displayName, step.stepId));
  const text = steps.length === 0 ? label : `${label}: ${allSteps ? steps.join(", ") : formatBlockingSteps(steps)}`;

  // Only the icon takes the color: as text, the warning and error colors are too pale to read.
  const content = (
    <Stack
      component="span"
      direction="row"
      sx={{ alignItems: "center", gap: 1 }}
      data-cy={`processingState-${shownState ?? "unknown"}`}>
      <Stack component="span" sx={{ color: shownState ? stateColor[shownState] : "primary.states.disabledBackground" }}>
        {shownState ? renderIcon(shownState) : <HelpOutlineIcon fontSize="small" />}
      </Stack>
      {text}
    </Stack>
  );
  return steps.length > 1 && !allSteps ? <Tooltip title={steps.join(", ")}>{content}</Tooltip> : content;
};
