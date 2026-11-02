import { FC, useContext } from "react";
import { useTranslation } from "react-i18next";
import { Box, Dialog, DialogActions, DialogContent, DialogTitle, Typography } from "@mui/material";
import { GridColDef } from "@mui/x-data-grid";
import { DeliveryReactionResponse, ReactionKind, ReactionState } from "../../api/generated";
import useFetch from "../../hooks/useFetch.ts";
import { useLocalized } from "../../hooks/useLocalized.ts";
import { AlertContext } from "../alert/alertContext";
import { Button } from "../buttons";
import GeopilotDataGrid from "./geopilotDataGrid.tsx";
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
  const { localized } = useLocalized();
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

  const columns: GridColDef<DeliveryReactionResponse>[] = [
    {
      field: "reactionId",
      headerName: t("reaction"),
      flex: 1,
      minWidth: 180,
      display: "flex",
      renderCell: ({ row }) => (
        <Box sx={{ py: 1 }}>
          {localized(row.displayName ?? undefined, row.reactionId)}
          {/* The log and the execution protocol name a reaction by its id. */}
          {row.displayName && (
            <Typography variant="body2" component="div" sx={{ color: "text.secondary" }}>
              {row.reactionId}
            </Typography>
          )}
        </Box>
      ),
    },
    {
      field: "kind",
      headerName: t("kind"),
      width: 140,
      display: "flex",
      valueGetter: (kind: ReactionKind) =>
        t(kind === ReactionKind.Transfer ? "reactionKindTransfer" : "reactionKindNotification"),
    },
    {
      field: "state",
      headerName: t("state"),
      width: 150,
      display: "flex",
      renderCell: ({ row }) => <ReactionStateLabel state={row.state} />,
    },
    { field: "attempts", headerName: t("attempts"), type: "number", width: 90, display: "flex" },
    {
      field: "time",
      headerName: t("time"),
      flex: 1,
      minWidth: 200,
      display: "flex",
      valueGetter: (_value, reaction) => describeTime(reaction),
    },
    { field: "message", headerName: t("message"), flex: 1.5, minWidth: 200, display: "flex" },
  ];

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
    <Dialog open={true} onClose={onClose} maxWidth={false} data-cy="forwarding-dialog">
      <DialogTitle>{t("forwardingDialogTitle", { id: deliveryId })}</DialogTitle>
      <DialogContent>
        <GeopilotDataGrid
          name="reactions"
          rows={reactions}
          columns={columns}
          getRowId={(reaction: DeliveryReactionResponse) => reaction.reactionId}
          getRowHeight={() => "auto"}
          autoHeight
          autosizeOnMount
          autosizeOptions={{ includeHeaders: true, includeOutliers: true }}
          autoPageSize={false}
          hideFooter
        />
      </DialogContent>
      <DialogActions>
        {canReset && <Button label="reset" variant="outlined" onClick={reset} data-cy="resetReactions-button" />}
        <Button label="close" variant="contained" onClick={onClose} />
      </DialogActions>
    </Dialog>
  );
};
