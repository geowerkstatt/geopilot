import { PipelineRunGroupResponse, PipelineRunSummaryResponse } from "../../../api/generated";

/**
 * A row of the list of pipeline runs: the newest attempt of a delivery, standing for all its attempts, or one of the
 * earlier attempts, shown below the newest while the delivery is expanded.
 */
export type PipelineRunRow =
  | (PipelineRunSummaryResponse & { kind: "latest"; attemptCount: number })
  | (PipelineRunSummaryResponse & { kind: "earlier" });

/** The rows of a page: each delivery by its newest attempt, followed by the earlier ones when it is expanded. */
export const toPipelineRunRows = (
  groups: PipelineRunGroupResponse[],
  expanded: ReadonlySet<string>,
): PipelineRunRow[] =>
  groups.flatMap((group): PipelineRunRow[] => [
    { ...group.latest, kind: "latest", attemptCount: group.earlierAttempts.length + 1 },
    ...(expanded.has(group.latest.jobId)
      ? group.earlierAttempts.map((attempt): PipelineRunRow => ({ ...attempt, kind: "earlier" }))
      : []),
  ]);
