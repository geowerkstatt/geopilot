import {
  emptyPipelineRunFilters,
  parseJobId,
  readPipelineRunSearchParams,
  tabFromHash,
  toPipelineRunQuery,
  toPipelineRunSearchParams,
} from "../../src/pages/admin/pipelineRuns/pipelineRunQuery";

describe("pipeline run query", () => {
  it("sends only the page without filters", () => {
    expect(toPipelineRunQuery(emptyPipelineRunFilters, 2, 25)).to.equal("page=2&pageSize=25");
  });

  it("ends the period where the day after the last day starts", () => {
    const params = new URLSearchParams(
      toPipelineRunQuery({ ...emptyPipelineRunFilters, from: "2026-10-28", to: "2026-10-30" }, 0, 25),
    );

    expect(params.get("from")).to.equal(new Date("2026-10-28T00:00:00").toISOString());
    expect(params.get("to")).to.equal(new Date("2026-10-31T00:00:00").toISOString());
  });

  it("splits who processed into kind and id", () => {
    const person = new URLSearchParams(
      toPipelineRunQuery({ ...emptyPipelineRunFilters, declarer: "person:12" }, 0, 25),
    );
    const allClients = new URLSearchParams(
      toPipelineRunQuery({ ...emptyPipelineRunFilters, declarer: "machineClient" }, 0, 25),
    );

    expect(person.get("declarerKind")).to.equal("person");
    expect(person.get("declarerId")).to.equal("12");
    expect(allClients.get("declarerKind")).to.equal("machineClient");
    expect(allClients.has("declarerId")).to.equal(false);
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

  it("opens the tab the address names, and the steps for any other address", () => {
    expect(tabFromHash("#files")).to.equal("files");
    expect(tabFromHash("#pipeline")).to.equal("pipeline");
    expect(tabFromHash("")).to.equal("steps");
    expect(tabFromHash("#unknown")).to.equal("steps");
  });

  it("keeps filters and page in the address of the list", () => {
    const filters = { from: "2026-10-28", to: "2026-10-30", declarer: "person:12", mandateId: "5", state: "failed" };

    const params = toPipelineRunSearchParams(filters, 3);

    expect(readPipelineRunSearchParams(params)).to.deep.equal({ filters, page: 3 });
    expect(toPipelineRunSearchParams(emptyPipelineRunFilters, 0).toString()).to.equal("");
    expect(readPipelineRunSearchParams(new URLSearchParams("page=abc"))).to.deep.equal({
      filters: emptyPipelineRunFilters,
      page: 0,
    });
  });
});
