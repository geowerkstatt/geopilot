import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink } from "react-router-dom";
import { Link, Typography } from "@mui/material";
import { RecordingOutcome, RecordingOutcomeKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";

interface RecordingDeliveryLabelProps {
  outcome: RecordingOutcome;
  /** Make the delivery a link to its detail page. */
  linkDelivery?: boolean;
}

// A run that still runs or whose outcome is unknown has nothing to say about a delivery yet.
const deliveryLabelKey: Partial<Record<RecordingOutcomeKind, TranslationKey>> = {
  [RecordingOutcomeKind.DeliveredAndDeleted]: "recordingDeliveryDeleted",
  [RecordingOutcomeKind.DeclarationFailed]: "recordingDeliveryFailed",
  [RecordingOutcomeKind.NotDelivered]: "recordingDeliveryNotDelivered",
  [RecordingOutcomeKind.NotDeliverable]: "recordingDeliveryNotPossible",
  [RecordingOutcomeKind.NotDeclarable]: "recordingDeliveryNotPermitted",
};

/** What became of the delivery of a run: its id, or why there is none. */
export const RecordingDeliveryLabel: FC<RecordingDeliveryLabelProps> = ({ outcome, linkDelivery }) => {
  const { t } = useTranslation();

  if (outcome.kind === RecordingOutcomeKind.Delivered && linkDelivery) {
    return (
      <Link component={RouterLink} to={`/admin/deliveries/${outcome.deliveryId}`} data-cy="recordingDelivery">
        {outcome.deliveryId}
      </Link>
    );
  }

  const key = deliveryLabelKey[outcome.kind];
  const text =
    outcome.kind === RecordingOutcomeKind.Delivered
      ? String(outcome.deliveryId)
      : key && t(key, { id: outcome.deliveryId });
  // Inherits the font of where it stands, a grid cell or a summary field.
  return (
    text && (
      <Typography component="span" variant="inherit" data-cy="recordingDelivery">
        {text}
      </Typography>
    )
  );
};
