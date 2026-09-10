import { loginAsAdmin } from "./helpers/appHelpers.js";
import { assertCancelDoesNotDeleteDelivery, assertConfirmDeletesDelivery } from "./helpers/deliveryOverviewHelper.js";

describe("Admin Delivery Overview tests", () => {
  it("can delete delivery", () => {
    cy.intercept({ url: "/api/v1/delivery", method: "GET" }).as("deliveries");
    cy.intercept("DELETE", "/api/v1/delivery/*", { statusCode: 200 }).as("deleteDelivery");
    loginAsAdmin();

    // Delete delivery
    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Kelvin Spencer");

    // Sort by delivery date ascending
    cy.dataCy("deliveryOverview-grid").contains("Delivery date").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("2022");

    // Sort to get newest delivery first
    cy.dataCy("deliveryOverview-grid").contains("Delivered by").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Kelvin Spencer");

    // Sort by Mandate
    cy.dataCy("deliveryOverview-grid").contains("Mandate").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("Fantastic Fresh Tuna");

    // Sort by comment
    cy.dataCy("deliveryOverview-grid").contains("Comment").click();
    cy.dataCy("deliveryOverview-grid")
      .find(".MuiDataGrid-row")
      .first()
      .contains("heard about this on wonky radio, decided to give it a try.");

    // Sort by Id
    cy.dataCy("deliveryOverview-grid").contains("ID").click();
    cy.dataCy("deliveryOverview-grid").find(".MuiDataGrid-row").first().contains("1");

    assertCancelDoesNotDeleteDelivery();
    assertConfirmDeletesDelivery();

    cy.wait("@deliveries");
  });
});
