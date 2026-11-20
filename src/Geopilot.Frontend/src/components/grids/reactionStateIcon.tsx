import { FC } from "react";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import ScheduleIcon from "@mui/icons-material/Schedule";
import { Box, CircularProgress } from "@mui/material";
import { ReactionState } from "../../api/generated";
import { px2rem } from "../../appTheme.ts";
import { reactionStateColor } from "./reactionState.ts";

type ReactionStateIconSize = "small" | "medium";

interface ReactionStateIconProps {
  state: ReactionState;
  /** Small beside the name of the state, medium in the header of an entry, as large as the icon of a step. */
  size: ReactionStateIconSize;
}

// As wide as the circle the other icons draw.
const progressSize: Record<ReactionStateIconSize, string> = { small: px2rem(16), medium: px2rem(20) };

const renderIcon = (state: ReactionState, size: ReactionStateIconSize) => {
  switch (state) {
    case ReactionState.Pending:
      return <ScheduleIcon fontSize={size} />;
    case ReactionState.Running:
      return <CircularProgress size={progressSize[size]} color="inherit" />;
    case ReactionState.Done:
      return <CheckCircleOutlineIcon fontSize={size} />;
    case ReactionState.Error:
      return <ErrorOutlineIcon fontSize={size} />;
    case ReactionState.Deleted:
      return <RemoveCircleOutlineIcon fontSize={size} />;
  }
};

/**
 * The icon of the state of a reaction in its color. Hidden from screen readers: the name of the state stands beside it
 * or on the frame around it.
 */
export const ReactionStateIcon: FC<ReactionStateIconProps> = ({ state, size }) => (
  <Box component="span" aria-hidden sx={{ display: "inline-flex", color: reactionStateColor[state] }}>
    {renderIcon(state, size)}
  </Box>
);
