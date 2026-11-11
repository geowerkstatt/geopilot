import {
  emptyProcessingRunFilters,
  listQueryOf,
  listQueryState,
  parseJobId,
  readProcessingRunSearchParams,
  tabFromHash,
  toProcessingRunQuery,
  toProcessingRunSearchParams,
} from "../../src/pages/admin/processingRuns/processingRunQuery";

describe("pipeline run query", () => {
  it("sends only the page without filters", () => {
    expect(toProcessingRunQuery(emptyProcessingRunFilters, 2, 25)).to.equal("page=2&pageSize=25");
  });

  it("ends the period where the minute after the last minute starts", () => {
    const params = new URLSearchParams(
      toProcessingRunQuery({ ...emptyProcessingRunFilters, from: "2026-10-28T08:00", to: "2026-10-30T23:59" }, 0, 25),
    );

    expect(params.get("from")).to.equal(new Date("2026-10-28T08:00:00").toISOString());
    expect(params.get("to")).to.equal(new Date("2026-10-31T00:00:00").toISOString());
  });

  it("sends who processed as the ids of persons and machine clients", () => {
    const params = new URLSearchParams(
      toProcessingRunQuery(
        { ...emptyProcessingRunFilters, declarers: ["person:12", "machineClient:3", "person:14"] },
        0,
        25,
      ),
    );

    expect(params.getAll("userIds")).to.deep.equal(["12", "14"]);
    expect(params.getAll("machineClientIds")).to.deep.equal(["3"]);
  });

  it("sends every chosen mandate and state", () => {
    const params = new URLSearchParams(
      toProcessingRunQuery({ ...emptyProcessingRunFilters, mandateIds: ["5", "7"], states: ["failed", "open"] }, 0, 25),
    );

    expect(params.getAll("mandateIds")).to.deep.equal(["5", "7"]);
    expect(params.getAll("states")).to.deep.equal(["failed", "open"]);
  });

  it("reads a job id in the forms people paste it", () => {
    const jobId = "3f9c2a71-5b0e-4c1d-9a8f-0d2e6b7c1e21";

    expect(parseJobId(jobId)).to.equal(jobId);
    expect(parseJobId("3F9C2A71-5B0E-4C1D-9A8F-0D2E6B7C1E21")).to.equal(jobId);
    expect(parseJobId(" {3f9c2a71-5b0e-4c1d-9a8f-0d2e6b7c1e21} ")).to.equal(jobId);
    expect(parseJobId("3f9c2a715b0e4c1d9a8f0d2e6b7c1e21")).to.equal(jobId);
    expect(parseJobId("3f9c2a71")).to.equal(undefined);
    expect(parseJobId("")).to.equal(undefined);
  });

  it("keeps the query of the list a run was opened from, and none for a run opened elsewhere", () => {
    expect(listQueryOf(listQueryState("state=failed&page=2"))).to.equal("state=failed&page=2");
    expect(listQueryOf(undefined)).to.equal("");
    expect(listQueryOf({ listQuery: 3 })).to.equal("");
  });

  it("opens the tab the address names, and the steps for any other address", () => {
    expect(tabFromHash("#reactions")).to.equal("reactions");
    expect(tabFromHash("#pipeline")).to.equal("pipeline");
    expect(tabFromHash("")).to.equal("steps");
    expect(tabFromHash("#unknown")).to.equal("steps");
  });

  it("keeps filters and page in the address of the list", () => {
    const filters = {
      from: "2026-10-28T08:00",
      to: "2026-10-30T17:30",
      declarers: ["person:12", "machineClient:3"],
      mandateIds: ["5"],
      states: ["failed", "open"],
    };

    const params = toProcessingRunSearchParams(filters, 3);

    expect(readProcessingRunSearchParams(params)).to.deep.equal({ filters, page: 3 });
    expect(toProcessingRunSearchParams(emptyProcessingRunFilters, 0).toString()).to.equal("");
    expect(readProcessingRunSearchParams(new URLSearchParams("page=abc"))).to.deep.equal({
      filters: emptyProcessingRunFilters,
      page: 0,
    });
  });

  it("takes an unreadable time in the address as no filter", () => {
    const params = new URLSearchParams({ from: "abc", to: "2026-13-45T99:99", state: "failed" });

    const { filters, page } = readProcessingRunSearchParams(params);

    expect(filters).to.deep.equal({ ...emptyProcessingRunFilters, states: ["failed"] });
    expect(toProcessingRunQuery(filters, page, 25)).to.equal("page=0&pageSize=25&states=failed");
  });
});
