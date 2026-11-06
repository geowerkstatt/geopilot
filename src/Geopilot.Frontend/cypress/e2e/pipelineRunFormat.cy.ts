import { formatBlockingSteps, formatDuration } from "../../src/pages/admin/pipelineRuns/pipelineRunFormat";

describe("pipeline run format", () => {
  it("names the first blocking step and counts the others", () => {
    expect(formatBlockingSteps([])).to.equal("");
    expect(formatBlockingSteps(["XTF Validierung"])).to.equal("XTF Validierung");
    expect(formatBlockingSteps(["XTF Validierung", "Referenzdaten", "Paket"])).to.equal("XTF Validierung (+2)");
  });

  it("measures the duration in minutes and seconds, and only once the run has ended", () => {
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:17:05Z")).to.equal("2 min 5 s");
    expect(formatDuration("2026-10-30T08:15:00Z", "2026-10-30T08:15:42Z")).to.equal("42 s");
    expect(formatDuration("2026-10-30T08:15:00Z", null)).to.equal(undefined);
  });
});
