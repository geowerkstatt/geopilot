import { loginAsAdmin } from "./helpers/appHelpers.js";
import { addFile, stepIsActive, stepIsCompleted, stepIsLoading, uploadFile } from "./helpers/deliveryHelpers.js";
import { setSelect } from "./helpers/formHelpers.js";
import { handlePrompt } from "./helpers/promptHelpers.js";

describe("Delivery Overview tests", () => {
  it("can delete delivery", () => {
    cy.intercept({ url: "/api/v1/validation", method: "POST" }).as("upload");
    cy.intercept({ url: "/api/v1/validation/*", method: "GET" }).as("validation");
    cy.intercept({ url: "/api/v1/mandate?jobId=*", method: "GET" }).as("mandates");
    cy.intercept({ url: "/api/v1/delivery?mandateId=*", method: "GET" }).as("precursors");
    cy.intercept({ url: "/api/v1/delivery", method: "POST" }).as("submit");
    cy.intercept({ url: "/api/v1/delivery", method: "GET" }).as("deliveries");

    // Add delivery
    loginAsAdmin();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.wait("@mandates");
    cy.wait(500);
    setSelect("mandate", 0);
    setSelect("precursor", 0);
    cy.get('[data-cy="createDelivery-button"]').click();
    stepIsLoading("submit");
    cy.wait("@submit");
    stepIsCompleted("submit");
    stepIsActive("done");

    // Delete delivery
    cy.visit("/admin/delivery-overview");
    cy.wait("@deliveries");
    cy.get('[data-cy="delete-delivery-button"]').should("not.exist");
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').should("have.length", 10);
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').first().contains("Laurence Rosenbaum");
    cy.get('[data-cy="deliveryOverview-grid"]').contains("Delivery date").click();
    cy.get('[data-cy="deliveryOverview-grid"]').contains("Delivery date").click();
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').first().contains("Andreas Admin").click();
    cy.get('[data-cy="delete-delivery-button"]').should("exist");
    cy.get('[data-cy="delete-delivery-button"]').contains("Delete 1 delivery");
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').last().click();
    cy.get('[data-cy="delete-delivery-button"]').contains("Delete 2 deliveries");
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').last().click();
    cy.get('[data-cy="delete-delivery-button"]').contains("Delete 1 delivery");
    cy.get('[data-cy="delete-delivery-button"]').click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "cancel");
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').first().contains("Andreas Admin");
    cy.get('[data-cy="delete-delivery-button"]').click();
    handlePrompt("Do you really want to delete the delivery data? This action cannot be undone.", "delete");
    cy.get('[data-cy="deliveryOverview-grid"] .MuiDataGrid-row').first().contains("Andreas Admin").should("not.exist");
  });
});
