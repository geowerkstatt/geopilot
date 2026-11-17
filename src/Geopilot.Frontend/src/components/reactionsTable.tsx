import { FC } from "react";
import { useTranslation } from "react-i18next";
import ReplayIcon from "@mui/icons-material/Replay";
import { TableCell, TableRow, Typography } from "@mui/material";
import { DeliveryReactionResponse, ReactionKind, ReactionState } from "../api/generated";
import { useLocalized } from "../hooks/useLocalized.ts";
import { IconButton } from "./buttons.tsx";
import { DetailTable } from "./detailTable.tsx";
import { ReactionStateLabel } from "./grids/reactionStateLabel.tsx";

interface ReactionsTableProps {
  reactions: DeliveryReactionResponse[];
  /** Whether the delivery is forwarded. Until then its notifications wait. */
  forwarded: boolean;
  /** Makes a reaction that gave up due again. */
  onRetry: (reaction: DeliveryReactionResponse) => void;
}

const formatTime = (value: string) => new Date(value).toLocaleString();

/** The reactions of a delivery with how far each got, and the retry of those that gave up. */
export const ReactionsTable: FC<ReactionsTableProps> = ({ reactions, forwarded, onRetry }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();

  const describeTime = (reaction: DeliveryReactionResponse) => {
    // A notification starts once the delivery is forwarded, which is after the delete window at the earliest.
    if (reaction.kind === ReactionKind.Notification && reaction.state === ReactionState.Pending && !forwarded) {
      return t("reactionWaitsForForwarding");
    }
    if (reaction.nextAttemptAt) {
      // Before its first attempt a reaction waits for the delete window of the delivery to close.
      const key = reaction.attempts === 0 ? "reactionWaitsUntil" : "reactionNextAttemptAt";
      return t(key, { time: formatTime(reaction.nextAttemptAt) });
    }
    if (reaction.state === ReactionState.Running && reaction.lastAttemptAt) {
      return t("reactionRunningSince", { time: formatTime(reaction.lastAttemptAt) });
    }
    return reaction.finishedAt ? t("reactionFinishedAt", { time: formatTime(reaction.finishedAt) }) : "";
  };

  return (
    <DetailTable
      dataCy="reactions-table"
      columns={[
        { label: t("reaction") },
        { label: t("kind"), width: "14%" },
        { label: t("state"), width: "14%" },
        { label: t("attempts"), width: "9%" },
        { label: t("time"), width: "20%" },
        { label: t("message") },
        { label: "", width: "48px" },
      ]}>
      {reactions.map(reaction => (
        <TableRow key={reaction.reactionId}>
          <TableCell>
            {localized(reaction.displayName ?? undefined, reaction.reactionId)}
            {/* The log and the execution protocol name a reaction by its id. */}
            {reaction.displayName && (
              <Typography variant="body2" component="div" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                {reaction.reactionId}
              </Typography>
            )}
          </TableCell>
          <TableCell>
            {t(reaction.kind === ReactionKind.Transfer ? "reactionKindTransfer" : "reactionKindNotification")}
          </TableCell>
          <TableCell>
            <ReactionStateLabel state={reaction.state} />
          </TableCell>
          <TableCell>{reaction.attempts}</TableCell>
          <TableCell>{describeTime(reaction)}</TableCell>
          <TableCell>{localized(reaction.message ?? undefined)}</TableCell>
          <TableCell sx={{ paddingY: 0 }}>
            {reaction.state === ReactionState.Error && (
              <IconButton
                icon={<ReplayIcon data-cy="retryReaction" />}
                label="retry"
                size="small"
                onClick={() => onRetry(reaction)}
              />
            )}
          </TableCell>
        </TableRow>
      ))}
    </DetailTable>
  );
};
