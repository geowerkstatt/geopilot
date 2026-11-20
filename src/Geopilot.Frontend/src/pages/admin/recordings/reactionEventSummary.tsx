import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Chip, Stack, Typography } from "@mui/material";
import { TFunction } from "i18next";
import { ReactionEvent, ReactionKind, ReactionState, RecordingReactionEventResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { reactionStateLabelKey } from "../../../components/grids/reactionState.ts";
import { ReactionStateIcon } from "../../../components/grids/reactionStateIcon.tsx";
import { StateIconFrame } from "../../../components/stateIconFrame.tsx";
import { EntryHeader } from "./entryHeader.tsx";
import { formatDuration } from "./recordingFormat.ts";
import { StepTitle } from "./stepTitle.tsx";

interface ReactionEventSummaryProps {
  reactionEvent: RecordingReactionEventResponse;
}

const reactionKindKey: Record<ReactionKind, TranslationKey> = {
  [ReactionKind.Transfer]: "reactionKindTransfer",
  [ReactionKind.Notification]: "reactionKindNotification",
};

const reactionEventKey: Record<Exclude<ReactionEvent, typeof ReactionEvent.Attempt>, TranslationKey> = {
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

const formatEvent = (t: TFunction, reactionEvent: RecordingReactionEventResponse): string =>
  reactionEvent.event === ReactionEvent.Attempt
    ? t("reactionEventAttempt", { attempt: reactionEvent.attempt })
    : t(reactionEventKey[reactionEvent.event]);

/** When an event happened, and for an attempt how long it ran or that it still runs. */
const formatTiming = (t: TFunction, reactionEvent: RecordingReactionEventResponse): string => {
  const time = new Date(reactionEvent.occurredAt).toLocaleString(undefined, { dateStyle: "short", timeStyle: "short" });
  if (reactionEvent.event !== ReactionEvent.Attempt) {
    return t("reactionEventOn", { time });
  }
  const duration = formatDuration(reactionEvent.occurredAt, reactionEvent.finishedAt);
  return duration ? t("reactionAttemptTiming", { time, duration }) : t("reactionAttemptTimingRunning", { time });
};

/**
 * The header of an event of a reaction, laid out as the header of a step: its outcome as an icon, which reaction of
 * what kind, what happened, when it started and how long it ran.
 */
export const ReactionEventSummary: FC<ReactionEventSummaryProps> = ({ reactionEvent }) => {
  const { t } = useTranslation();
  const shownState = outcomeOf(reactionEvent);

  return (
    <EntryHeader
      icon={
        <StateIconFrame label={shownState ? t(reactionStateLabelKey[shownState]) : undefined}>
          {shownState && <ReactionStateIcon state={shownState} size="medium" />}
        </StateIconFrame>
      }
      title={
        <Stack direction="row" sx={{ alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
          <Typography>
            <StepTitle id={reactionEvent.reactionId} displayName={reactionEvent.reactionDisplayName ?? undefined} />
          </Typography>
          <Chip
            size="small"
            variant="outlined"
            label={t(reactionKindKey[reactionEvent.reactionKind])}
            sx={{ bgcolor: "transparent" }}
          />
          <Chip size="small" label={formatEvent(t, reactionEvent)} />
        </Stack>
      }
      aside={formatTiming(t, reactionEvent)}
    />
  );
};
