import { loginAsAdmin } from "./helpers/appHelpers";

// The list holds one delivery with two attempts, answered from a fixture.
describe("Pipeline runs", () => {
  beforeEach(() => {
    cy.intercept({ method: "GET", pathname: "/api/v1/pipelinerun" }, { fixture: "processingRunGroups.json" }).as(
      "runs",
    );
    loginAsAdmin();
    cy.visit("/admin/processing-runs");
    cy.wait("@runs");
  });

  it("shows how many attempts a delivery took", () => {
    cy.dataCy("processingRuns-grid").find(".MuiDataGrid-row").should("have.length", 1);
    cy.dataCy("processingRuns-grid").find('[data-field="attemptCount"][role="gridcell"]').should("have.text", "2");
  });
});

// The server answers with as many runs as the list asks for, out of more than fit on one page.
describe("Pipeline runs on a full page", () => {
  let requestedPageSizes: number[];

  beforeEach(() => {
    requestedPageSizes = [];
    cy.fixture("processingRunGroups.json").then(groups => {
      cy.intercept({ method: "GET", pathname: "/api/v1/pipelinerun" }, request => {
        const pageSize = Number(new URL(request.url).searchParams.get("pageSize"));
        requestedPageSizes.push(pageSize);
        request.reply({
          totalCount: 200,
          items: Array.from({ length: pageSize }, (_, index) => ({
            latest: { ...groups.items[0].latest, jobId: `00000000-0000-4000-8000-${String(index).padStart(12, "0")}` },
            earlierAttempts: [],
          })),
        });
      }).as("runs");
    });
    loginAsAdmin();
    cy.visit("/admin/processing-runs");
    cy.wait("@runs");
  });

  it("fits its page to its height like the other lists, asking only once", () => {
    cy.dataCy("processingRuns-grid")
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
