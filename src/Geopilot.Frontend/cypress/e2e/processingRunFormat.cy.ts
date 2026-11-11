import { TFunction } from "i18next";
import { InputKind, InputOrigin, PipelineRunInputResponse, ProcessingState } from "../../src/api/generated";
import {
  formatBlockingSteps,
  formatDuration,
  formatInputOrigin,
  formatInputValue,
  formatProcessingState,
  shownProcessingState,
} from "../../src/pages/admin/processingRuns/processingRunFormat";

// Echoes the key, so the assertions read the keys instead of a language. TFunction has overloads a lambda cannot match.
const t = ((key: string) => key) as unknown as TFunction;

const input = (fields: Partial<PipelineRunInputResponse>): PipelineRunInputResponse => ({
  parameter: "transferFile",
  origin: InputOrigin.Definition,
  kind: InputKind.Value,
  ...fields,
});

describe("pipeline run format", () => {
  it("shows a value as its text, a file with its size when known, and nothing for a skipped step", () => {
    expect(formatInputValue(input({ value: "2.4" }))).to.equal("2.4");
    expect(formatInputValue(input({ value: null }))).to.equal("null");
    expect(formatInputValue(input({ kind: InputKind.File, fileName: "Sauge.xtf", size: 2048 }))).to.equal(
      "Sauge.xtf (2 KB)",
    );
    expect(formatInputValue(input({ kind: InputKind.File, fileName: "Sauge.xtf", size: 0 }))).to.equal(
      "Sauge.xtf (0 B)",
    );
    expect(formatInputValue(input({ kind: InputKind.File, fileName: "validation.log", size: null }))).to.equal(
      "validation.log",
    );
    expect(formatInputValue(input({ kind: InputKind.Absent, origin: InputOrigin.StepOutput }))).to.equal(undefined);
  });

  it("names where an input came from", () => {
    expect(formatInputOrigin(t, InputOrigin.Definition)).to.equal("inputOriginDefinition");
    expect(formatInputOrigin(t, InputOrigin.StepOutput)).to.equal("inputOriginStepOutput");
    expect(formatInputOrigin(t, InputOrigin.Resource)).to.equal("inputOriginResource");
    expect(formatInputOrigin(t, InputOrigin.Upload)).to.equal("inputOriginUpload");
    expect(formatInputOrigin(t, InputOrigin.Configuration)).to.equal("inputOriginConfiguration");
  });

  it("shows a run without terminal state as running only while it runs", () => {
    expect(shownProcessingState(ProcessingState.Failed, true)).to.equal(ProcessingState.Failed);
    expect(shownProcessingState(null, true)).to.equal(ProcessingState.Running);
    expect(shownProcessingState(null, false)).to.equal(undefined);
    expect(formatProcessingState(t, ProcessingState.Success)).to.equal("stepStateFinished");
    expect(formatProcessingState(t, undefined)).to.equal("processingStateUnknown");
  });

  it("names the first blocking step and counts the others", () => {
    expect(formatBlockingSteps([])).to.equal("");
    expect(formatBlockingSteps(["XTF Validierung"])).to.equal("XTF Validierung");
    expect(formatBlockingSteps(["XTF Validierung", "Referenzdaten", "Paket"])).to.equal("XTF Validierung (+2)");
  });

  it("measures the duration in minutes and seconds, below a second in milliseconds, and only once the run has ended", () => {
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:17:05Z")).to.equal("2 min 5 s");
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:15:42Z")).to.equal("42 s");
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:15:00.003Z")).to.equal("3 ms");
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:15:00.999Z")).to.equal("999 ms");
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:15:01Z")).to.equal("1 s");
    expect(formatDuration("2026-10-30T08:15:00Z", null)).to.equal(undefined);
  });
});
