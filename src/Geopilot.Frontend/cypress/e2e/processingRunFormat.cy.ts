import { TFunction } from "i18next";
import { ClientKind } from "../../src/api/generated";
import {
  formatBlockingSteps,
  formatClientKind,
  formatDuration,
} from "../../src/pages/admin/processingRuns/processingRunFormat";

// Echoes the key, so the assertions read the keys instead of a language. TFunction has overloads a lambda cannot match.
const t = ((key: string) => key) as unknown as TFunction;

describe("pipeline run format", () => {
  it("names the kind of client a run was started over, and nothing for an unknown kind", () => {
    expect(formatClientKind(t, ClientKind.WebClient)).to.equal("clientKindWeb");
    expect(formatClientKind(t, ClientKind.ApiClient)).to.equal("clientKindApi");
    expect(formatClientKind(t, ClientKind.Unknown)).to.equal(undefined);
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
