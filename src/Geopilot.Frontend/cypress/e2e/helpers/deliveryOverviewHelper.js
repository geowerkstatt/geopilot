import { handlePrompt } from "./promptHelpers.js";

export function assertCancelDoesNotDeleteDelivery() {
  cy.intercept("DELETE", "/api/v1/delivery/*", () => {
    throw new Error("Cancel should not trigger delete");
  }).as("deleteDelivery");

  cy.dataCy("deliveryOverview-grid")
    .find(".MuiDataGrid-row")
    .first()
    .find('[data-testid="DeleteOutlinedIcon"]')
    .click();
  handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "cancel");

  // make sure no delete request was sent in this time frame
  cy.wait(400);
}

export function assertConfirmDeletesDelivery() {
  cy.intercept("DELETE", "/api/v1/delivery/*", { statusCode: 200 }).as("deleteDelivery");

  cy.dataCy("deliveryOverview-grid")
    .find(".MuiDataGrid-row")
    .first()
    .find('[data-testid="DeleteOutlinedIcon"]')
    .click();
  handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "delete");

  cy.wait("@deleteDelivery");
}
