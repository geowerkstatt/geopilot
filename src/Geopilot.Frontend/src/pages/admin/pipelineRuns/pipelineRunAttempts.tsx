import { FC, KeyboardEvent, MouseEvent } from "react";
import { useTranslation } from "react-i18next";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import SubdirectoryArrowRightIcon from "@mui/icons-material/SubdirectoryArrowRight";
import { Stack, Typography } from "@mui/material";
import { IconButton } from "../../../components/buttons.tsx";
import { PipelineRunRow } from "./pipelineRunRows.ts";

interface PipelineRunAttemptsProps {
  row: PipelineRunRow;
  expanded: boolean;
  onToggle: () => void;
}

/**
 * The attempts of a delivery as a row of the list shows them: on the newest of several, a switch for the earlier ones
 * and how many there are in all; on an earlier one, a mark that it belongs to the row above.
 */
export const PipelineRunAttempts: FC<PipelineRunAttemptsProps> = ({ row, expanded, onToggle }) => {
  const { t } = useTranslation();

  if (row.kind === "earlier") {
    return <SubdirectoryArrowRightIcon fontSize="small" titleAccess={t("earlierAttempt")} />;
  }
  if (row.attemptCount === 1) {
    return null;
  }

  const toggle = (event: MouseEvent<HTMLButtonElement>) => {
    // A click on the row opens the run; the switch only shows or hides the earlier attempts.
    event.stopPropagation();
    onToggle();
  };

  const keepSpace = (event: KeyboardEvent<HTMLButtonElement>) => {
    // The grid moves through its cells on space and cancels the key, which would keep space from pressing the switch.
    if (event.key === " ") {
      event.stopPropagation();
    }
  };

  return (
    <Stack direction="row" sx={{ alignItems: "center", gap: 0.5 }}>
      <IconButton
        icon={expanded ? <ExpandMoreIcon /> : <ChevronRightIcon />}
        label={expanded ? "hideEarlierAttempts" : "showEarlierAttempts"}
        size="small"
        aria-expanded={expanded}
        onClick={toggle}
        onKeyDown={keepSpace}
      />
      <Typography variant="body2">{row.attemptCount}</Typography>
    </Stack>
  );
};
