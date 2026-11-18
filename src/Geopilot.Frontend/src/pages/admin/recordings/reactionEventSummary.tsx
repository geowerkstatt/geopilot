import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack, Typography } from "@mui/material";
import { ReactionEvent, ReactionKind, RecordingReactionEventResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { reactionStateLabelKey } from "../../../components/grids/reactionState.ts";
import { ReactionStateIcon } from "../../../components/grids/reactionStateIcon.tsx";
import { formatDuration } from "./recordingFormat.ts";
import { TechnicalText } from "./technicalText.tsx";

interface ReactionEventSummaryProps {
  reactionEvent: RecordingReactionEventResponse;
}

const reactionKindKey: Record<ReactionKind, TranslationKey> = {
  [ReactionKind.Transfer]: "reactionKindTransfer",
  [ReactionKind.Notification]: "reactionKindNotification",
};

const reactionEventKey: Record<ReactionEvent, TranslationKey> = {
  [ReactionEvent.Attempt]: "reactionEventAttempt",
  [ReactionEvent.Reset]: "reactionEventReset",
  [ReactionEvent.Deletion]: "reactionEventDeletion",
};

/**
 * The header of an event of a reaction, laid out as the header of a step: the state the reaction was left in, what
 * happened to which reaction and of what kind, and when, with the time an attempt took.
 */
export const ReactionEventSummary: FC<ReactionEventSummaryProps> = ({ reactionEvent }) => {
  const { t } = useTranslation();
  const state = reactionEvent.state ? t(reactionStateLabelKey[reactionEvent.state]) : undefined;
  const duration = formatDuration(reactionEvent.occurredAt, reactionEvent.finishedAt);

  return (
    <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
      {reactionEvent.state && <ReactionStateIcon state={reactionEvent.state} />}
      <Typography sx={{ flex: "1 1 0" }}>
        {t(reactionEventKey[reactionEvent.event], { attempt: reactionEvent.attempt })}
        <Typography component="span" variant="body2" sx={{ color: "text.secondary", marginLeft: 1 }}>
          <TechnicalText>{reactionEvent.reactionId}</TechnicalText>
        </Typography>
        <Typography component="span" variant="body2" sx={{ color: "text.secondary", marginLeft: 1 }}>
          {t(reactionKindKey[reactionEvent.reactionKind])}
        </Typography>
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {new Date(reactionEvent.occurredAt).toLocaleString()}
      </Typography>
      {state && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {duration ? t("stepStateAfterDuration", { state, duration }) : state}
        </Typography>
      )}
    </Stack>
  );
};
