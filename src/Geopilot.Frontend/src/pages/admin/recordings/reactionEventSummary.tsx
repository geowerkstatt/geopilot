import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { ReactionEvent, ReactionKind, ReactionState, RecordingReactionEventResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme.ts";
import { reactionStateLabelKey } from "../../../components/grids/reactionState.ts";
import { ReactionStateIcon } from "../../../components/grids/reactionStateIcon.tsx";
import { formatDuration } from "./recordingFormat.ts";
import { StepTitle } from "./stepTitle.tsx";

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

// The protocol keeps the state the reaction was left in. A failed attempt with another one to follow leaves it
// pending, yet the attempt itself failed, and that is what its header has to tell.
const outcomeOf = (reactionEvent: RecordingReactionEventResponse) =>
  reactionEvent.event === ReactionEvent.Attempt &&
  reactionEvent.finishedAt &&
  reactionEvent.state === ReactionState.Pending
    ? ReactionState.Error
    : reactionEvent.state;

/**
 * The header of an event of a reaction, laid out as the header of a step: how an attempt ended, or the state a reset or
 * a deletion left the reaction in, what happened to which reaction and of what kind, and when, with the time an
 * attempt took.
 */
export const ReactionEventSummary: FC<ReactionEventSummaryProps> = ({ reactionEvent }) => {
  const { t } = useTranslation();
  const shownState = outcomeOf(reactionEvent);
  const state = shownState ? t(reactionStateLabelKey[shownState]) : undefined;
  const duration = formatDuration(reactionEvent.occurredAt, reactionEvent.finishedAt);

  const ending = state && duration ? t("stepStateAfterDuration", { state, duration }) : (state ?? duration);

  // The spaces keep the words apart for a screen reader and in copied text.
  return (
    <Stack direction="row" sx={{ alignItems: "center", gap: 2, flexWrap: "wrap", width: "100%", paddingRight: 2 }}>
      {/* The slot keeps its width without an icon, so the titles line up. */}
      <Box component="span" sx={{ display: "inline-flex", justifyContent: "center", width: px2rem(20), flexShrink: 0 }}>
        {shownState && <ReactionStateIcon state={shownState} />}
      </Box>
      <Typography sx={{ flex: "1 1 0" }}>
        {t(reactionEventKey[reactionEvent.event], { attempt: reactionEvent.attempt })}{" "}
        <StepTitle id={reactionEvent.reactionId} displayName={reactionEvent.reactionDisplayName ?? undefined} />{" "}
        <Typography component="span" variant="body2" sx={{ color: "text.secondary", marginLeft: 0.5 }}>
          {t(reactionKindKey[reactionEvent.reactionKind])}
        </Typography>
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary" }}>
        {new Date(reactionEvent.occurredAt).toLocaleString()}
      </Typography>
      {ending && (
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {ending}
        </Typography>
      )}
    </Stack>
  );
};
