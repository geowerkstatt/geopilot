import { FC } from "react";
import { useTranslation } from "react-i18next";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import ScheduleIcon from "@mui/icons-material/Schedule";
import { CircularProgress, Stack } from "@mui/material";
import { ReactionState } from "../../api/generated";
import { px2rem } from "../../appTheme.ts";

interface ReactionStateLabelProps {
  state: ReactionState;
}

const stateLabelKey: Record<ReactionState, string> = {
  [ReactionState.Pending]: "forwardingStatePending",
  [ReactionState.Running]: "forwardingStateRunning",
  [ReactionState.Done]: "forwardingStateDone",
  [ReactionState.Error]: "forwardingStateError",
  [ReactionState.Deleted]: "forwardingStateDeleted",
};

const renderIcon = (state: ReactionState) => {
  switch (state) {
    case ReactionState.Pending:
      return <ScheduleIcon fontSize="small" color="action" />;
    case ReactionState.Running:
      return <CircularProgress size={px2rem(16)} aria-hidden />;
    case ReactionState.Done:
      return <CheckCircleOutlineIcon fontSize="small" color="primary" />;
    case ReactionState.Error:
      return <ErrorOutlineIcon fontSize="small" color="error" />;
    case ReactionState.Deleted:
      return <RemoveCircleOutlineIcon fontSize="small" color="disabled" />;
  }
};

/**
 * The state of a reaction, or the state of the forwarding of a delivery, as an icon with its name. A span, so it can
 * also be the content of a button.
 */
export const ReactionStateLabel: FC<ReactionStateLabelProps> = ({ state }) => {
  const { t } = useTranslation();

  return (
    <Stack component="span" direction="row" sx={{ alignItems: "center", gap: 0.5 }} data-cy={`reactionState-${state}`}>
      {renderIcon(state)}
      {t(stateLabelKey[state])}
    </Stack>
  );
};
