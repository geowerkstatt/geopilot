import {
  ClientKind,
  PipelineRunGroupResponse,
  PipelineRunOutcomeKind,
  PipelineRunSummaryResponse,
  ProcessingState,
} from "../../src/api/generated";
import { ProcessingRunRow, toProcessingRunRows } from "../../src/pages/admin/processingRuns/processingRunRows";

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

// What a row says about the grouping: its run, and how many attempts it stands for or that it is an earlier one.
const describeRows = (rows: ProcessingRunRow[]) =>
  rows.map(row => (row.kind === "latest" ? `${row.jobId} of ${row.attemptCount}` : `${row.jobId} earlier`));

describe("pipeline run rows", () => {
  it("shows a run without earlier attempts as one row", () => {
    expect(describeRows(toProcessingRunRows([group("a")], new Set()))).to.deep.equal(["a of 1"]);
  });

  it("shows only the newest attempt of a collapsed group, counting all", () => {
    expect(describeRows(toProcessingRunRows([group("c", "b", "a")], new Set()))).to.deep.equal(["c of 3"]);
  });

  it("lists the earlier attempts of an expanded group right below its newest", () => {
    expect(describeRows(toProcessingRunRows([group("c", "b", "a"), group("d")], new Set(["c"])))).to.deep.equal([
      "c of 3",
      "b earlier",
      "a earlier",
      "d of 1",
    ]);
  });

  it("expands only the groups asked for", () => {
    expect(describeRows(toProcessingRunRows([group("c", "b"), group("e", "d")], new Set(["e"])))).to.deep.equal([
      "c of 2",
      "e of 2",
      "d earlier",
    ]);
  });
});
