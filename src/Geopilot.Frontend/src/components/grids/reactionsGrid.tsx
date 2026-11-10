import { FC } from "react";
import { useTranslation } from "react-i18next";
import ReplayIcon from "@mui/icons-material/Replay";
import { Box, Tooltip, Typography } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef } from "@mui/x-data-grid";
import { DeliveryReactionResponse, ReactionKind, ReactionState } from "../../api/generated";
import { useLocalized } from "../../hooks/useLocalized.ts";
import GeopilotDataGrid from "./geopilotDataGrid.tsx";
import { ReactionStateLabel } from "./reactionStateLabel.tsx";

interface ReactionsGridProps {
  reactions: DeliveryReactionResponse[];
  /** Whether the delivery is forwarded. Until then its notifications wait. */
  forwarded: boolean;
  /** Makes a reaction that gave up due again. */
  onRetry: (reaction: DeliveryReactionResponse) => void;
}

const formatTime = (value: string) => new Date(value).toLocaleString();

/** The reactions of a delivery with how far each got, and the retry of those that gave up. */
export const ReactionsGrid: FC<ReactionsGridProps> = ({ reactions, forwarded, onRetry }) => {
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
            <Typography variant="body2" component="div" sx={{ color: "text.secondary", fontStyle: "italic" }}>
              {row.reactionId}
            </Typography>
          )}
        </Box>
      ),
    },
    {
      field: "kind",
      headerName: t("kind"),
      width: 150,
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
      minWidth: 220,
      display: "flex",
      valueGetter: (_value, reaction) => describeTime(reaction),
    },
    {
      field: "message",
      headerName: t("message"),
      flex: 1.5,
      minWidth: 200,
      display: "flex",
      valueGetter: (message: DeliveryReactionResponse["message"]) => localized(message ?? undefined),
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      renderCell: params => (
        <GridActionsCell {...params}>
          {params.row.state === ReactionState.Error && (
            <GridActionsCellItem
              icon={
                <Tooltip title={t("retry")} key={`retry-${params.id}`}>
                  <ReplayIcon data-cy="retryReaction" color="primary" />
                </Tooltip>
              }
              label={t("retry")}
              onClick={() => onRetry(params.row)}
            />
          )}
        </GridActionsCell>
      ),
    },
  ];

  return (
    <GeopilotDataGrid
      name="reactions"
      rows={reactions}
      columns={columns}
      getRowId={(reaction: DeliveryReactionResponse) => reaction.reactionId}
      getRowHeight={() => "auto"}
      autoHeight
      autoPageSize={false}
      hideFooter
    />
  );
};
