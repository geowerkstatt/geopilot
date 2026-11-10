import type { Interception } from "cypress/types/net-stubbing";
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

  it("shows the earlier attempts below the newest without opening a run", () => {
    cy.dataCy("processingRuns-grid").find(".MuiDataGrid-row").should("have.length", 1);

    cy.dataCy("showEarlierAttempts-button").click();

    cy.dataCy("processingRuns-grid").find(".MuiDataGrid-row").should("have.length", 2);
    cy.location("pathname").should("eq", "/admin/processing-runs");
  });

  it("leaves the space key to the switch", () => {
    cy.dataCy("showEarlierAttempts-button").focus();

    // A dispatched key never presses the button, but it does show whether the grid takes the key away: the grid
    // moves through its cells on space and cancels the key, so a keyboard user could not toggle the switch.
    cy.focused().then($switch => {
      const space = new KeyboardEvent("keydown", { key: " ", code: "Space", bubbles: true, cancelable: true });
      $switch[0].dispatchEvent(space);
      expect(space.defaultPrevented, "space cancelled").to.equal(false);
    });
    cy.dataCy("showEarlierAttempts-button").should("have.focus");
  });

  it("opens with a single query", () => {
    // The row shows once the last query is answered. A grid that fits its page to its height asks a second time
    // right after the first, with the size it measured.
    cy.dataCy("processingRuns-grid").find(".MuiDataGrid-row").should("have.length", 1);

    cy.get<Interception[]>("@runs.all").then(calls => {
      const queries = new Set(calls.map(call => new URL(call.request.url).search));
      expect([...queries]).to.have.length(1);
    });
  });
});
