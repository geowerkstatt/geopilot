import { isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import { setInput, setSelect } from "./helpers/formHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";

describe("Mandate tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/mandates");
    isSelectedNavItem(`admin-mandates-nav`, "admin-navigation");
  });

  it("displays the mandates in a list with pagination", () => {
    cy.get('[data-cy="mandates-grid"]').should("exist");
    cy.get('[data-cy="mandates-grid"] .MuiDataGrid-row').should("have.length", 10);
    cy.get(".MuiTablePagination-select").click();
    cy.get("li.MuiTablePagination-menuItem").contains("5").click();
    cy.get('[data-cy="mandates-grid"] .MuiDataGrid-row').should("have.length", 5);
    cy.get('[data-cy="mandates-grid"] .MuiDataGrid-row').first().contains("Handmade Soft Cheese");
    cy.get('[data-cy="mandates-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').click();
    cy.get('[data-cy="mandates-grid"] .MuiDataGrid-row').first().contains("Incredible Plastic Ball");
  });

  it("checks for unsaved changes when navigating", () => {
    const randomMandateName = `Mandate-${Math.random().toString(36).substring(2, 15)}`;

    cy.get('[data-cy="addMandate-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    cy.get('[data-cy="backToMandates-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("exist");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    cy.get('[data-cy="backToMandates-button"]').click();
    isPromptVisible(false);
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates`);
    });
    cy.get('[data-cy="mandates-grid"]').should("exist");

    cy.get('[data-cy="addMandate-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    setInput("name", randomMandateName);
    cy.contains("Description").click();
    cy.wait(500); // Click outside the input field and wait to trigger the validation.
    cy.get('[data-cy="save-button"]').should("be.disabled");
    cy.get('[data-cy="admin-users-nav"]').click();
    checkPromptActions(["cancel", "reset"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "cancel");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    cy.get('[data-cy="admin-mandates-nav"]').click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates`);
    });
    cy.get('[data-cy="mandates-grid"]').should("exist");

    cy.get('[data-cy="addMandate-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    setInput("name", randomMandateName);
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("precursor", 0, 3);
    setSelect("partialDelivery", 1, 2);
    setSelect("comment", 1, 3);
    cy.get('[data-cy="save-button"]').should("be.enabled");
    openTool("delivery");
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/`);
    });

    cy.visit("/admin/mandates");
    cy.get('[data-cy="mandates-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').click();
    cy.get('[data-cy="mandates-grid"] .MuiDataGrid-row').first().contains(randomMandateName);
  });
});
