import {
  ClientKind,
  PipelineRunGroupResponse,
  PipelineRunOutcomeKind,
  PipelineRunSummaryResponse,
  ProcessingState,
} from "../../src/api/generated";
import { toProcessingRunRows } from "../../src/pages/admin/processingRuns/processingRunRows";

const run = (jobId: string): PipelineRunSummaryResponse => ({
  jobId,
  startedAt: "2026-11-06T08:00:00Z",
  terminalAt: "2026-11-06T08:01:00Z",
  clientKind: ClientKind.ApiClient,
  clientReference: "AV-1",
  pipelineId: "pipe_a",
  terminalState: ProcessingState.Success,
  outcome: { kind: PipelineRunOutcomeKind.Delivered, blockingSteps: [] },
});

const group = (latest: string, ...earlierAttempts: string[]): PipelineRunGroupResponse => ({
  latest: run(latest),
  earlierAttempts: earlierAttempts.map(jobId => run(jobId)),
});

describe("pipeline run rows", () => {
  it("shows each delivery by its newest attempt, counting all", () => {
    const rows = toProcessingRunRows([group("c", "b", "a"), group("d")]);

    expect(rows.map(row => `${row.jobId} of ${row.attemptCount}`)).to.deep.equal(["c of 3", "d of 1"]);
  });
});
