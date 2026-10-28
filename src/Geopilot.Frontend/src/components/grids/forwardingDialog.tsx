import { FC, useContext } from "react";
import { useTranslation } from "react-i18next";
import {
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from "@mui/material";
import { DeliveryReactionResponse, ReactionKind, ReactionState } from "../../api/generated";
import useFetch from "../../hooks/useFetch.ts";
import { AlertContext } from "../alert/alertContext";
import { Button } from "../buttons";
import { ReactionStateLabel } from "./reactionStateLabel.tsx";

interface ForwardingDialogProps {
  deliveryId: number;
  reactions: DeliveryReactionResponse[];
  onClose: () => void;
  /** Called once the failed reactions are due again, so the caller can show their new state. */
  onReset: () => void;
}

const formatTime = (value: string) => new Date(value).toLocaleString();

/** The reactions of a delivery with how far each got, and the reset of those that gave up. */
export const ForwardingDialog: FC<ForwardingDialogProps> = ({ deliveryId, reactions, onClose, onReset }) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { showAlert } = useContext(AlertContext);
  const canReset = reactions.some(reaction => reaction.state === ReactionState.Error);

  const describeTime = (reaction: DeliveryReactionResponse) => {
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

  const reset = () =>
    fetchApi(`/api/v1/delivery/${deliveryId}/reactions/reset`, {
      method: "POST",
      errorMessageLabel: "reactionsResetError",
    })
      .then(() => {
        showAlert(t("reactionsReset"), "success");
        onReset();
      })
      // useFetch has shown why; the dialog stays open, so the reset can be tried again.
      .catch(() => undefined);

  return (
    <Dialog open={true} onClose={onClose} maxWidth="lg" fullWidth data-cy="forwarding-dialog">
      <DialogTitle>{t("forwardingDialogTitle", { id: deliveryId })}</DialogTitle>
      <DialogContent>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>{t("reaction")}</TableCell>
              <TableCell>{t("kind")}</TableCell>
              <TableCell>{t("state")}</TableCell>
              <TableCell>{t("attempts")}</TableCell>
              <TableCell>{t("time")}</TableCell>
              <TableCell>{t("message")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {reactions.map(reaction => (
              <TableRow key={reaction.reactionId} data-cy={`reaction-${reaction.reactionId}`}>
                <TableCell>{reaction.reactionId}</TableCell>
                <TableCell>
                  {t(reaction.kind === ReactionKind.Transfer ? "reactionKindTransfer" : "reactionKindNotification")}
                </TableCell>
                <TableCell>
                  <ReactionStateLabel state={reaction.state} />
                </TableCell>
                <TableCell>{reaction.attempts}</TableCell>
                <TableCell>{describeTime(reaction)}</TableCell>
                <TableCell sx={{ wordBreak: "break-word" }}>{reaction.message}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </DialogContent>
      <DialogActions>
        {canReset && <Button label="reset" variant="outlined" onClick={reset} data-cy="resetReactions-button" />}
        <Button label="close" variant="contained" onClick={onClose} />
      </DialogActions>
    </Dialog>
  );
};
