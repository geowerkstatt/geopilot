import { isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import {
  evaluateAutocomplete,
  evaluateInput,
  evaluateSelect,
  hasError,
  setAutocomplete,
  setInput,
  setSelect,
} from "./helpers/formHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";

const getRandomManadateName = () => `Mandate-${Math.random().toString(36).substring(2, 15)}`;

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
    const randomMandateName = getRandomManadateName();

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

  it("adds new mandate", () => {
    const randomMandateName = getRandomManadateName();
    cy.intercept({ url: "/api/v1/mandate", method: "POST" }).as("saveNew");

    cy.get('[data-cy="addMandate-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });

    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    hasError("name", true);
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    hasError("precursor", true);
    hasError("partialDelivery", true);
    hasError("comment", true);

    setInput("name", randomMandateName);
    hasError("name", false);
    cy.get('[data-cy="reset-button"]').should("be.enabled");

    setInput("extent-bottom-left-longitude", "7.3");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-bottom-left-latitude", "47.13");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-upper-right-longitude", "8.052");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-upper-right-latitude", "47.46");
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);
    setSelect("precursor", 0, 3);
    hasError("precursor", false);
    setSelect("partialDelivery", 1, 2);
    hasError("partialDelivery", false);
    setSelect("comment", 1, 3);
    hasError("comment", false);

    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);

    cy.get('[data-cy="save-button"]').should("be.enabled");

    setAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Brown and Sons"]);
    setAutocomplete("fileTypes", ".csv");
    setAutocomplete("fileTypes", ".xtf");
    evaluateAutocomplete("fileTypes", [".csv", ".xtf"]);

    cy.get('[data-cy="reset-button"]').click();
    evaluateInput("name", "");
    evaluateAutocomplete("organisations", []);
    evaluateAutocomplete("fileTypes", []);
    evaluateInput("extent-bottom-left-longitude", "");
    evaluateInput("extent-bottom-left-latitude", "");
    evaluateInput("extent-upper-right-longitude", "");
    evaluateInput("extent-upper-right-latitude", "");
    evaluateSelect("precursor", "");
    evaluateSelect("partialDelivery", "");
    evaluateSelect("comment", "");

    setInput("name", randomMandateName);
    setAutocomplete("organisations", "Brown and Sons");
    setAutocomplete("fileTypes", ".csv");
    setAutocomplete("fileTypes", ".xtf");
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("precursor", 0, 3);
    setSelect("partialDelivery", 1, 2);
    setSelect("comment", 1, 3);

    cy.get('[data-cy="save-button"]').click();
    cy.wait("@saveNew");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });

    // TODO: Check if the new mandate can be edited right away.

    cy.get('[data-cy="backToMandates-button"]').click();
    cy.get('[data-cy="mandates-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').click();
    cy.contains(randomMandateName);
  });
});
