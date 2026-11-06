import { loginAsUploader } from "./helpers/appHelpers";
import { assertCancelDoesNotDeleteDelivery, assertConfirmDeletesDelivery } from "./helpers/deliveryOverviewHelper";

describe("User Delivery Overview tests", () => {
  it("user can navigate to uploaded deliveries", () => {
    loginAsUploader();

    cy.dataCy("loggedInUser-button").click();
    cy.dataCy("my-deliveries-nav").should("exist");
    cy.dataCy("admin-nav").should("not.exist");

    cy.dataCy("my-deliveries-nav").click();

    cy.location("pathname").should("eq", "/user/deliveries");
  });

  it("user can delete uploaded delivery", () => {
    cy.intercept({ url: "/api/v1/delivery/uploads", method: "GET" }).as("deliveries");
    loginAsUploader();

    cy.visit("/user/deliveries");
    cy.wait("@deliveries");

    // Newest delivery first
    cy.dataCy("deliveryOverview-grid")
      .find('[role="columnheader"][data-field="date"]')
      .should("have.attr", "aria-sort", "descending");

    // Who delivered, so the own deliveries stand apart from those of a machine client the user is responsible for.
    cy.dataCy("deliveryOverview-grid").find('[role="columnheader"][data-field="userName"]').contains("Delivered by");
    // The name of the identity provider, which replaced the seeded one when the uploader signed in.
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Ursula Uploader");

    // Unsorted, in the order of the server
    cy.dataCy("deliveryOverview-grid").contains("Delivery date").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Fantastic Fresh Tuna");

    // Sort by delivery date ascending
    cy.dataCy("deliveryOverview-grid").contains("Delivery date").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("2023");

    // Sort by Mandate
    cy.dataCy("deliveryOverview-grid").contains("Mandate").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Fantastic Fresh Tuna");

    // Sort by comment
    cy.dataCy("deliveryOverview-grid").contains("Comment").click();
    cy.dataCy("deliveryOverview-grid")
      .find(".MuiDataGrid-row")
      .first()
      .contains("I saw one of these in Saint Lucia and I bought one.");

    assertCancelDoesNotDeleteDelivery();
    assertConfirmDeletesDelivery();

    cy.wait("@deliveries");
  });
});
