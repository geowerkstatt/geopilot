import { PipelineRunGroupResponse, PipelineRunSummaryResponse } from "../../../api/generated";

/** A row of the list of pipeline runs: the newest attempt of a delivery, standing for all its attempts. */
export type ProcessingRunRow = PipelineRunSummaryResponse & { attemptCount: number };

/** The rows of a page: each delivery by its newest attempt, with how many attempts it had. */
export const toProcessingRunRows = (groups: PipelineRunGroupResponse[]): ProcessingRunRow[] =>
  groups.map(group => ({ ...group.latest, attemptCount: group.earlierAttempts.length + 1 }));
