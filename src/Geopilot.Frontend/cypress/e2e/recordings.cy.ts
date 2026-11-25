import { loginAsAdmin } from "./helpers/appHelpers";

// The list holds one delivery with two attempts, answered from a fixture.
describe("Recordings", () => {
  beforeEach(() => {
    cy.intercept({ method: "GET", pathname: "/api/v1/recording" }, { fixture: "recordingGroups.json" }).as("runs");
    loginAsAdmin();
    cy.visit("/admin/recordings");
    cy.wait("@runs");
  });

  it("shows how many attempts a delivery took", () => {
    cy.dataCy("recordings-grid").find(".MuiDataGrid-row").should("have.length", 1);
    cy.dataCy("recordings-grid").find('[data-field="attemptCount"][role="gridcell"]').should("have.text", "2");
  });

  it("opens a recording with the details button of its row", () => {
    cy.dataCy("recordings-grid").find(".MuiDataGrid-row").should("have.css", "cursor", "pointer");
    cy.dataCy("recordings-grid").find(".MuiDataGrid-row").first().find('[data-cy="details"]').click();
    cy.location("pathname").should("eq", "/admin/recordings/6f7cdfa3-c20a-44bc-bc6c-8771bfadcc8a");
  });

  it("filters by several states at once", () => {
    // The states in the order of RecordingStateFilter: failed is the fourth, open the sixth.
    cy.dataCy("recordingFilter-state").click();
    // The latest request, as the grid may ask once more with the previous filters while it fits its page to a new
    // height. It goes out once the list shows the filters, so the next pick adds to them.
    const latestRequestHasStates = (states: string[]) =>
      cy.get<{ request: { url: string } }[]>("@runs.all").should(calls => {
        const latestQuery = new URL(calls[calls.length - 1].request.url).searchParams;
        expect(latestQuery.getAll("states")).to.deep.equal(states);
      });

    cy.get('[role="listbox"] [role="option"]').eq(3).click();
    latestRequestHasStates(["failed"]);
    cy.get('[role="listbox"] [role="option"]').eq(5).click();
    latestRequestHasStates(["failed", "open"]);

    cy.location("search").should("equal", "?state=failed&state=open");
  });
});

// The server answers with as many runs as the list asks for, out of more than fit on one page.
describe("Recordings on a full page", () => {
  let requestedPageSizes: number[];

  beforeEach(() => {
    requestedPageSizes = [];
    cy.fixture("recordingGroups.json").then(groups => {
      cy.intercept({ method: "GET", pathname: "/api/v1/recording" }, request => {
        const pageSize = Number(new URL(request.url).searchParams.get("pageSize"));
        requestedPageSizes.push(pageSize);
        request.reply({
          totalCount: 200,
          items: Array.from({ length: pageSize }, (_, index) => ({
            latest: { ...groups.items[0].latest, jobId: `00000000-0000-4000-8000-${String(index).padStart(12, "0")}` },
            attemptCount: 1,
          })),
        });
      }).as("runs");
    });
    loginAsAdmin();
    cy.visit("/admin/recordings");
    cy.wait("@runs");
  });

  it("fits its page to its height like the other lists, asking only once", () => {
    cy.dataCy("recordings-grid")
      .find(".MuiDataGrid-virtualScroller")
      .should($scroller => {
        // A grid that fits its page to its height reports the size it measured; asked before that, the list asks
        // twice, the second time with another size.
        const pageSizes = [...new Set(requestedPageSizes)];
        expect(pageSizes, "page sizes asked for").to.have.length(1);

        // Every run of the page shows without scrolling, and there is no room for another one.
        const bottom = $scroller[0].getBoundingClientRect().bottom;
        const visibleRows = [...$scroller[0].querySelectorAll(".MuiDataGrid-row")]
          .map(row => row.getBoundingClientRect())
          .filter(row => row.bottom <= bottom + 1);
        expect(visibleRows, "runs shown without scrolling").to.have.length(pageSizes[0]);
        expect(bottom - visibleRows[visibleRows.length - 1].bottom, "room left below").to.be.lessThan(
          visibleRows[0].height,
        );
      });
  });
});
