import { FC } from "react";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutlineOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import RemoveCircleOutlineIcon from "@mui/icons-material/RemoveCircleOutlineOutlined";
import ScheduleIcon from "@mui/icons-material/Schedule";
import { Box, CircularProgress } from "@mui/material";
import { ReactionState } from "../../api/generated";
import { px2rem } from "../../appTheme.ts";
import { reactionStateColor } from "./reactionState.ts";

interface ReactionStateIconProps {
  state: ReactionState;
}

const renderIcon = (state: ReactionState) => {
  switch (state) {
    case ReactionState.Pending:
      return <ScheduleIcon fontSize="small" />;
    case ReactionState.Running:
      return <CircularProgress size={px2rem(16)} color="inherit" />;
    case ReactionState.Done:
      return <CheckCircleOutlineIcon fontSize="small" />;
    case ReactionState.Error:
      return <ErrorOutlineIcon fontSize="small" />;
    case ReactionState.Deleted:
      return <RemoveCircleOutlineIcon fontSize="small" />;
  }
};

/** The icon of the state of a reaction in its color. Hidden from screen readers: the name of the state stands beside it. */
export const ReactionStateIcon: FC<ReactionStateIconProps> = ({ state }) => (
  <Box component="span" aria-hidden sx={{ display: "inline-flex", color: reactionStateColor[state] }}>
    {renderIcon(state)}
  </Box>
);
