import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack } from "@mui/material";
import { ReactionState } from "../../api/generated";
import { reactionStateColor, reactionStateLabelKey } from "./reactionState.ts";
import { ReactionStateIcon } from "./reactionStateIcon.tsx";

interface ReactionStateLabelProps {
  state: ReactionState;
}

/** The state of a reaction, or of the reactions of one kind summed up, as an icon with its name. */
export const ReactionStateLabel: FC<ReactionStateLabelProps> = ({ state }) => {
  const { t } = useTranslation();

  return (
    <Stack
      component="span"
      direction="row"
      sx={{ alignItems: "center", gap: 1, color: reactionStateColor[state] }}
      data-cy={`reactionState-${state}`}>
      <ReactionStateIcon state={state} />
      {t(reactionStateLabelKey[state])}
    </Stack>
  );
};
