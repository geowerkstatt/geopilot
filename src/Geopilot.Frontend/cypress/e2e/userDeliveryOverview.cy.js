import { loginAsUploader } from "./helpers/appHelpers.js";
import { handlePrompt } from "./helpers/promptHelpers.js";

describe("Delivery Overview tests", () => {
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
    cy.intercept("DELETE", "/api/v1/delivery/*", { statusCode: 200 }).as("deleteDelivery");
    loginAsUploader();

    cy.visit("/user/deliveries");
    cy.wait("@deliveries");
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

    cy.dataCy("deliveryOverview-grid")
      .find(".MuiDataGrid-row")
      .first()
      .find('[data-testid="DeleteOutlinedIcon"]')
      .click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "cancel");
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");
    cy.dataCy("deliveryOverview-grid")
      .find(".MuiDataGrid-row")
      .first()
      .find('[data-testid="DeleteOutlinedIcon"]')
      .click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "delete");

    cy.wait("@deleteDelivery");
    cy.wait("@deliveries");
  });
});
