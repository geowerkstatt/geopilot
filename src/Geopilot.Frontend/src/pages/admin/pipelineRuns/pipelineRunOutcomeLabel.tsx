import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import { Link, Stack, Tooltip, Typography } from "@mui/material";
import { PipelineRunOutcome, PipelineRunOutcomeKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { formatBlockingSteps } from "./pipelineRunFormat.ts";

interface PipelineRunOutcomeLabelProps {
  outcome: PipelineRunOutcome;
  /** Name every step that kept the run from allowing a delivery, rather than the first and how many more. */
  allSteps?: boolean;
  /** Make the delivery a link to its detail page. */
  linkDelivery?: boolean;
}

const outcomeLabelKey: Record<PipelineRunOutcomeKind, TranslationKey> = {
  [PipelineRunOutcomeKind.Delivered]: "pipelineRunOutcomeDelivered",
  [PipelineRunOutcomeKind.DeliveredAndDeleted]: "pipelineRunOutcomeDeliveredAndDeleted",
  [PipelineRunOutcomeKind.NotDelivered]: "pipelineRunOutcomeNotDelivered",
  [PipelineRunOutcomeKind.NotDeliverable]: "pipelineRunOutcomeNotDeliverable",
  [PipelineRunOutcomeKind.NotDeclarable]: "pipelineRunOutcomeNotDeclarable",
  [PipelineRunOutcomeKind.Running]: "pipelineRunOutcomeRunning",
  [PipelineRunOutcomeKind.Unknown]: "pipelineRunOutcomeUnknown",
};

/** What became of a pipeline run: its delivery with the state of the forwarding, or why there is none. */
export const PipelineRunOutcomeLabel: FC<PipelineRunOutcomeLabelProps> = ({ outcome, allSteps, linkDelivery }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const label = t(outcomeLabelKey[outcome.kind], { id: outcome.deliveryId });

  if (outcome.kind === PipelineRunOutcomeKind.Delivered) {
    return (
      <Stack component="span" direction="row" sx={{ alignItems: "center", gap: 2 }} data-cy="pipelineRunOutcome">
        {linkDelivery ? (
          <Link component={RouterLink} to={`/admin/delivery-overview/${outcome.deliveryId}`}>
            {label}
          </Link>
        ) : (
          <Typography component="span" variant="inherit">
            {label}
          </Typography>
        )}
        {outcome.forwardingState && <ReactionStateLabel state={outcome.forwardingState} />}
      </Stack>
    );
  }

  // A restriction does not stop the pipeline, so several steps can keep the run from allowing a delivery.
  const steps = outcome.blockingSteps.map(step => localized(step.displayName, step.stepId));
  const text = steps.length === 0 ? label : `${label}: ${allSteps ? steps.join(", ") : formatBlockingSteps(steps)}`;

  // Inherits the font of where it stands, a grid cell or a summary field.
  const content = (
    <Typography component="span" variant="inherit" data-cy="pipelineRunOutcome">
      {text}
    </Typography>
  );
  return steps.length > 1 && !allSteps ? <Tooltip title={steps.join(", ")}>{content}</Tooltip> : content;
};
