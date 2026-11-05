import { FC } from "react";
import { useTranslation } from "react-i18next";
import { TableCell, TableRow } from "@mui/material";
import { PipelineRunReactionEventResponse, ReactionEvent, ReactionKind } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { DetailTable } from "./detailTable.tsx";

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
    <SummaryField labelVariant="h5" label="reactionEvents">
      <DetailTable
        columns={[
          { label: t("time"), width: "18%" },
          { label: t("reaction"), width: "16%" },
          { label: t("kind"), width: "12%" },
          { label: t("event"), width: "16%" },
          { label: t("state"), width: "14%" },
          { label: t("message") },
        ]}>
        {events.map((reactionEvent, index) => (
          <TableRow key={index}>
            <TableCell>{new Date(reactionEvent.occurredAt).toLocaleString()}</TableCell>
            <TableCell>{reactionEvent.reactionId}</TableCell>
            <TableCell>{t(reactionKindKey[reactionEvent.reactionKind])}</TableCell>
            <TableCell>{t(reactionEventKey[reactionEvent.event], { attempt: reactionEvent.attempt })}</TableCell>
            <TableCell>{reactionEvent.state && <ReactionStateLabel state={reactionEvent.state} />}</TableCell>
            <TableCell>{reactionEvent.message}</TableCell>
          </TableRow>
        ))}
      </DetailTable>
    </SummaryField>
  );
};
