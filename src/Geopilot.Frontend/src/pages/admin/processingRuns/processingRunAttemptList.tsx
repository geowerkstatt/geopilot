import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation } from "react-router-dom";
import { Link, TableCell, TableRow } from "@mui/material";
import { PipelineRunOutcomeKind, PipelineRunSummaryResponse } from "../../../api/generated";
import { DetailTable } from "../../../components/detailTable.tsx";
import { ProcessingRunDeliveryLabel } from "./processingRunDeliveryLabel.tsx";
import { formatDuration } from "./processingRunFormat.ts";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";

interface ProcessingRunAttemptListProps {
  /** The attempts of the delivery, newest first. */
  attempts: PipelineRunSummaryResponse[];
  /** The job id of the attempt the page shows. */
  shownJobId: string;
}

// The link of a row covers the whole row, so a click anywhere on it opens the attempt.
const rowLinkSx = { "&::after": { content: '""', position: "absolute", inset: 0 } };

/**
 * The attempts of a delivery, numbered from the first. The row of every other attempt links to its own page, which
 * opens on the same tab and goes back to the same list.
 */
export const ProcessingRunAttemptList: FC<ProcessingRunAttemptListProps> = ({ attempts, shownJobId }) => {
  const { t } = useTranslation();
  const location = useLocation();

  // shortcut: lists every attempt; limit the height if a client keeps sending one reference for all its deliveries.
  return (
    <DetailTable
      dataCy="processingRunAttempts"
      columns={[
        { label: t("attempt"), width: "12%" },
        { label: t("processingStarted"), width: "26%" },
        { label: t("state"), width: "36%" },
        { label: t("delivery") },
      ]}>
      {attempts.map((attempt, index) => {
        const isShown = attempt.jobId === shownJobId;
        const startedAt = new Date(attempt.startedAt).toLocaleString();
        return (
          <TableRow
            key={attempt.jobId}
            selected={isShown}
            aria-current={isShown ? "page" : undefined}
            sx={{ position: "relative" }}
            data-cy={`processingRunAttempt-${attempt.jobId}`}>
            <TableCell>{attempts.length - index}</TableCell>
            <TableCell>
              {isShown ? (
                startedAt
              ) : (
                <Link
                  component={RouterLink}
                  to={{ pathname: `/admin/processing-runs/${attempt.jobId}`, hash: location.hash }}
                  state={location.state}
                  color="inherit"
                  underline="none"
                  sx={rowLinkSx}>
                  {startedAt}
                </Link>
              )}
            </TableCell>
            <TableCell>
              {/* All steps, as the link over the row would keep a tooltip with the rest from opening. */}
              <ProcessingStateLabel
                state={attempt.terminalState}
                running={attempt.outcome.kind === PipelineRunOutcomeKind.Running}
                blockingSteps={attempt.outcome.blockingSteps}
                preflightFailed={attempt.outcome.preflightFailed}
                allSteps
                duration={formatDuration(attempt.startedAt, attempt.terminalAt)}
              />
            </TableCell>
            <TableCell>
              <ProcessingRunDeliveryLabel outcome={attempt.outcome} />
            </TableCell>
          </TableRow>
        );
      })}
    </DetailTable>
  );
};
