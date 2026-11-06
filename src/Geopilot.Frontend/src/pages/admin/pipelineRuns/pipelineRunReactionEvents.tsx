import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Table, TableBody, TableCell, TableHead, TableRow } from "@mui/material";
import { PipelineRunReactionEventResponse, ReactionEvent, ReactionKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import { TechnicalText } from "./technicalText.tsx";

interface PipelineRunReactionEventsProps {
  events: PipelineRunReactionEventResponse[];
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

/** What happened to the reactions of the delivery a run produced, in the order it happened. */
export const PipelineRunReactionEvents: FC<PipelineRunReactionEventsProps> = ({ events }) => {
  const { t } = useTranslation();

  return (
    <Table size="small" data-cy="pipelineRunReactionEvents">
      <TableHead>
        <TableRow>
          <TableCell>{t("time")}</TableCell>
          <TableCell>{t("reaction")}</TableCell>
          <TableCell>{t("event")}</TableCell>
          <TableCell>{t("state")}</TableCell>
          <TableCell>{t("message")}</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {events.map((reactionEvent, index) => (
          <TableRow key={index}>
            <TableCell>{new Date(reactionEvent.occurredAt).toLocaleString()}</TableCell>
            <TableCell>
              <TechnicalText>{reactionEvent.reactionId}</TechnicalText>
              {` · ${t(reactionKindKey[reactionEvent.reactionKind])}`}
            </TableCell>
            <TableCell>{t(reactionEventKey[reactionEvent.event], { attempt: reactionEvent.attempt })}</TableCell>
            <TableCell>{reactionEvent.state && <ReactionStateLabel state={reactionEvent.state} />}</TableCell>
            <TableCell sx={{ overflowWrap: "anywhere" }}>{reactionEvent.message}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
};
