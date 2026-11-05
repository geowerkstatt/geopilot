import { TFunction } from "i18next";
import { ClientKind, DeclarerKind } from "../../src/api/generated";
import {
  formatBlockingSteps,
  formatDeclarer,
  formatDuration,
} from "../../src/pages/admin/pipelineRuns/pipelineRunFormat";

// Echoes the key, so the assertions read the keys instead of a language. TFunction has overloads a lambda cannot match.
const t = ((key: string) => key) as unknown as TFunction;

describe("pipeline run format", () => {
  it("names who processed with the kind of client in parentheses, and leaves out an unknown kind", () => {
    const declarer = { kind: DeclarerKind.Person, id: 12, name: "Erika Muster" };
    expect(formatDeclarer(t, declarer, ClientKind.WebClient)).to.equal("Erika Muster (clientKindWeb)");
    expect(formatDeclarer(t, null, ClientKind.ApiClient)).to.equal("anonymous (clientKindApi)");
    expect(formatDeclarer(t, declarer, ClientKind.Unknown)).to.equal("Erika Muster");
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
