import { FC } from "react";
import { useTranslation } from "react-i18next";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import ScheduleIcon from "@mui/icons-material/Schedule";
import { CircularProgress, Stack } from "@mui/material";
import { ReactionState } from "../../api/generated";
import { TranslationKey } from "../../appInterfaces";
import { px2rem } from "../../appTheme.ts";

interface ReactionStateLabelProps {
  state: ReactionState;
}

const stateLabelKey: Record<ReactionState, TranslationKey> = {
  [ReactionState.Pending]: "reactionStatePending",
  [ReactionState.Running]: "reactionStateRunning",
  [ReactionState.Done]: "reactionStateDone",
  [ReactionState.Error]: "reactionStateError",
  [ReactionState.Deleted]: "reactionStateDeleted",
};

const stateColor: Record<ReactionState, string> = {
  [ReactionState.Pending]: "primary.main",
  [ReactionState.Running]: "primary.main",
  [ReactionState.Done]: "primary.main",
  [ReactionState.Error]: "error.main",
  [ReactionState.Deleted]: "primary.states.disabledBackground",
};

// The icons take the color of the label.
const renderIcon = (state: ReactionState) => {
  switch (state) {
    case ReactionState.Pending:
      return <ScheduleIcon fontSize="small" />;
    case ReactionState.Running:
      return <CircularProgress size={px2rem(16)} color="inherit" aria-hidden />;
    case ReactionState.Done:
      return <CheckCircleOutlineIcon fontSize="small" />;
    case ReactionState.Error:
      return <ErrorOutlineIcon fontSize="small" />;
    case ReactionState.Deleted:
      return <RemoveCircleOutlineIcon fontSize="small" />;
  }
};

/** The state of a reaction, or of the reactions of one kind summed up, as an icon with its name. */
export const ReactionStateLabel: FC<ReactionStateLabelProps> = ({ state }) => {
  const { t } = useTranslation();

  return (
    <Stack
      component="span"
      direction="row"
      sx={{ alignItems: "center", gap: 1, color: stateColor[state] }}
      data-cy={`reactionState-${state}`}>
      {renderIcon(state)}
      {t(stateLabelKey[state])}
    </Stack>
  );
};
