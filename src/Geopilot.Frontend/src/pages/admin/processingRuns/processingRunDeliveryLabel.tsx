import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import { Link, Typography } from "@mui/material";
import { PipelineRunOutcome, PipelineRunOutcomeKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";

interface ProcessingRunDeliveryLabelProps {
  outcome: PipelineRunOutcome;
  /** Make the delivery a link to its detail page. */
  linkDelivery?: boolean;
}

// A run that still runs or whose outcome is unknown has nothing to say about a delivery yet.
const deliveryLabelKey: Partial<Record<PipelineRunOutcomeKind, TranslationKey>> = {
  [PipelineRunOutcomeKind.DeliveredAndDeleted]: "pipelineRunDeliveryDeleted",
  [PipelineRunOutcomeKind.NotDelivered]: "pipelineRunDeliveryNotDelivered",
  [PipelineRunOutcomeKind.NotDeliverable]: "pipelineRunDeliveryNotPossible",
  [PipelineRunOutcomeKind.NotDeclarable]: "pipelineRunDeliveryNotPermitted",
};

/** What became of the delivery of a run: its id, or why there is none. */
export const ProcessingRunDeliveryLabel: FC<ProcessingRunDeliveryLabelProps> = ({ outcome, linkDelivery }) => {
  const { t } = useTranslation();

  if (outcome.kind === PipelineRunOutcomeKind.Delivered && linkDelivery) {
    return (
      <Link component={RouterLink} to={`/admin/deliveries/${outcome.deliveryId}`} data-cy="pipelineRunDelivery">
        {outcome.deliveryId}
      </Link>
    );
  }

  const key = deliveryLabelKey[outcome.kind];
  const text =
    outcome.kind === PipelineRunOutcomeKind.Delivered
      ? String(outcome.deliveryId)
      : key && t(key, { id: outcome.deliveryId });
  // Inherits the font of where it stands, a grid cell or a summary field.
  return (
    text && (
      <Typography component="span" variant="inherit" data-cy="pipelineRunDelivery">
        {text}
      </Typography>
    )
  );
};
