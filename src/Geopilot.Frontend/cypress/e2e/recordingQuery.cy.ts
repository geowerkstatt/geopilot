import { createLoader } from "nuqs";
import { listQueryOf, listQueryState } from "../../src/pages/admin/listQuery";
import {
  emptyRecordingFilters,
  parseJobId,
  recordingSearchParams,
  recordingUrlKeys,
  serializeRecordingSearch,
  tabFromHash,
  toRecordingQuery,
} from "../../src/pages/admin/recordings/recordingQuery";

// Reads the address of the list the way useQueryStates does.
const loadRecordingSearch = createLoader(recordingSearchParams, { urlKeys: recordingUrlKeys });

describe("recording query", () => {
  it("sends only the page without filters", () => {
    expect(toRecordingQuery(emptyRecordingFilters, 2, 25)).to.equal("page=2&pageSize=25");
  });

  it("ends the period where the minute after the last minute starts", () => {
    const params = new URLSearchParams(
      toRecordingQuery({ ...emptyRecordingFilters, from: "2026-10-28T08:00", to: "2026-10-30T23:59" }, 0, 25),
    );

    expect(params.get("from")).to.equal(new Date("2026-10-28T08:00:00").toISOString());
    expect(params.get("to")).to.equal(new Date("2026-10-31T00:00:00").toISOString());
  });

  it("sends who processed as the ids of users and machine clients", () => {
    const params = new URLSearchParams(
      toRecordingQuery({ ...emptyRecordingFilters, startedBy: ["user:12", "machineClient:3", "user:14"] }, 0, 25),
    );

    expect(params.getAll("userIds")).to.deep.equal(["12", "14"]);
    expect(params.getAll("machineClientIds")).to.deep.equal(["3"]);
  });

  it("sends every chosen mandate and state", () => {
    const params = new URLSearchParams(
      toRecordingQuery({ ...emptyRecordingFilters, mandateIds: ["5", "7"], states: ["failed", "open"] }, 0, 25),
    );

    expect(params.getAll("mandateIds")).to.deep.equal(["5", "7"]);
    expect(params.getAll("states")).to.deep.equal(["failed", "open"]);
  });

  it("reads a job id in either case, without the spaces around it", () => {
    const jobId = "3f9c2a71-5b0e-4c1d-9a8f-0d2e6b7c1e21";

    expect(parseJobId(jobId)).to.equal(jobId);
    expect(parseJobId(" 3F9C2A71-5B0E-4C1D-9A8F-0D2E6B7C1E21 ")).to.equal("3F9C2A71-5B0E-4C1D-9A8F-0D2E6B7C1E21");
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
    const search = {
      from: "2026-10-28T08:00",
      to: "2026-10-30T17:30",
      startedBy: ["user:12", "machineClient:3"],
      mandateIds: ["5"],
      states: ["failed", "open"],
      page: 3,
    };

    expect(loadRecordingSearch(serializeRecordingSearch(search))).to.deep.equal(search);
    expect(serializeRecordingSearch({ ...emptyRecordingFilters, page: 0 })).to.equal("");
    expect(loadRecordingSearch("?page=abc")).to.deep.equal({ ...emptyRecordingFilters, page: 0 });
  });

  it("reads an address of the list with repeated keys as before", () => {
    const address = "?startedBy=user:12&startedBy=machineClient:3&mandate=5&mandate=7&state=failed&state=open&page=2";

    const search = loadRecordingSearch(address);

    expect(search).to.deep.equal({
      ...emptyRecordingFilters,
      startedBy: ["user:12", "machineClient:3"],
      mandateIds: ["5", "7"],
      states: ["failed", "open"],
      page: 2,
    });
    const written = new URLSearchParams(serializeRecordingSearch(search));
    expect(written.getAll("state")).to.deep.equal(["failed", "open"]);
    expect(written.getAll("mandate")).to.deep.equal(["5", "7"]);
    expect(written.getAll("startedBy")).to.deep.equal(["user:12", "machineClient:3"]);
  });

  it("takes an unreadable time in the address as no filter", () => {
    const { page, ...filters } = loadRecordingSearch("?from=abc&to=2026-13-45T99:99&state=failed");

    expect(filters).to.deep.equal({ ...emptyRecordingFilters, states: ["failed"] });
    expect(toRecordingQuery(filters, page, 25)).to.equal("page=0&pageSize=25&states=failed");
  });

  it("drops the starters, mandates and states in the address that the API cannot take", () => {
    const search = loadRecordingSearch(
      "?startedBy=user:12&startedBy=foo&startedBy=machineClient:&startedBy=user:1234567890" +
        "&mandate=5&mandate=abc&mandate=-1&state=open&state=bogus",
    );

    expect(search.startedBy).to.deep.equal(["user:12"]);
    expect(search.mandateIds).to.deep.equal(["5"]);
    expect(search.states).to.deep.equal(["open"]);
  });

  it("shows the first page for a page in the address that the API cannot take", () => {
    expect(loadRecordingSearch("?page=-2").page).to.equal(0);
    expect(loadRecordingSearch("?page=1000001").page).to.equal(0);
    expect(loadRecordingSearch("?page=1000000").page).to.equal(1_000_000);
  });
});
