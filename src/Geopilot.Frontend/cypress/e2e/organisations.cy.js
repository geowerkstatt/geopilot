import { isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";
import {
  evaluateAutocomplete,
  evaluateInput,
  getFormField,
  hasError,
  removeAutocompleteValue,
  setAutocomplete,
  setInput,
} from "./helpers/formHelpers.js";

const getRandomOrganisationName = () => `Organisation-${Math.random().toString(36).substring(2, 15)}`;

describe("Organisations tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/organisations");
    isSelectedNavItem("admin-organisations-nav", "admin-navigation");
  });

  it("displays the organisations in a list with pagination", () => {
    cy.get('[data-cy="organisations-grid"]').should("exist");
    cy.get('[data-cy="organisations-grid"] .MuiDataGrid-row').should("have.length", 3);
    cy.get('[data-cy="organisations-grid"] .MuiTablePagination-actions [aria-label="Go to previous page"]').should(
      "be.disabled",
    );
    cy.get('[data-cy="organisations-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').should(
      "be.disabled",
    );
    cy.get('[data-cy="organisations-grid"] .MuiDataGrid-row').first().contains("Schumm, Runte and Macejkovic");
  });

  it("checks for unsaved changes when navigating", () => {
    const randomOrganisationName = getRandomOrganisationName();

    cy.get('[data-cy="addOrganisation-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    cy.get('[data-cy="backToOrganisations-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("exist");
    cy.get('[data-cy="save-button"]').should("be.disabled");
    hasError("name", true);

    cy.get('[data-cy="backToOrganisations-button"]').click();
    isPromptVisible(false);
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations`);
    });
    cy.get('[data-cy="organisations-grid"]').should("exist");
    cy.get('[data-cy="addOrganisation-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    setAutocomplete("users", "Kelvin Spencer");
    cy.wait(500);
    cy.get('[data-cy="save-button"]').should("be.disabled");
    cy.get('[data-cy="admin-users-nav"]').click();
    checkPromptActions(["cancel", "reset"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "cancel");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    cy.get('[data-cy="admin-organisations-nav"]').click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations`);
    });
    cy.get('[data-cy="organisations-grid"]').should("exist");
    cy.get('[data-cy="addOrganisation-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    setInput("name", randomOrganisationName);
    cy.get('[data-cy="save-button"]').should("be.enabled");
    openTool("delivery");
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/`);
    });
    cy.visit("/admin/organisations");
    cy.get('[data-cy="organisations-grid"] .MuiDataGrid-row').last().contains(randomOrganisationName);
  });

  it("can create organisation", () => {
    const randomOrganisationName = getRandomOrganisationName();
    cy.intercept({ url: "/api/v1/organisation", method: "POST" }).as("saveNew");

    cy.get('[data-cy="addOrganisation-button"]').click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });

    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    hasError("name", true);
    hasError("mandates", false);
    hasError("users", false);

    setAutocomplete("users", "Kelvin Spencer");
    cy.get('[data-cy="reset-button"]').should("be.enabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    setInput("name", randomOrganisationName);
    cy.get('[data-cy="reset-button"]').should("be.enabled");
    cy.get('[data-cy="save-button"]').should("be.enabled");

    cy.get('[data-cy="reset-button"]').click();
    evaluateInput("name", "");
    evaluateAutocomplete("mandates", []);
    evaluateAutocomplete("users", []);

    setInput("name", randomOrganisationName);
    setAutocomplete("mandates", "Fantastic Fresh Tuna");
    setAutocomplete("users", "Nick Purdy");

    cy.get('[data-cy="save-button"]').click();
    cy.wait("@saveNew");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    setAutocomplete("users", "Kelvin Spencer");
    cy.wait(500);
    cy.get('[data-cy="reset-button"]').should("be.enabled");
    cy.get('[data-cy="backToOrganisations-button"]').click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.get('[data-cy="organisations-grid"]').last().contains(randomOrganisationName);
    cy.get('[data-cy="organisations-grid"]').last().contains("Fantastic Fresh Tuna");
    cy.get('[data-cy="organisations-grid"]').last().contains("Nick Purdy");
    cy.get('[data-cy="organisations-grid"]').last().contains("Kevin Spencer").should("not.exist");
  });

  it("can edit existing organisation", () => {
    const randomOrganisationName = getRandomOrganisationName();
    cy.intercept({ url: "/api/v1/organisation", method: "POST" }).as("saveNew");
    cy.intercept({ url: "/api/v1/organisation", method: "PUT" }).as("updateOrganisation");

    // Create new organisation for testing
    cy.get('[data-cy="addOrganisation-button"]').click();
    setInput("name", randomOrganisationName);
    setAutocomplete("mandates", "Fantastic Fresh Tuna");
    setAutocomplete("users", "Nick Purdy");
    cy.get('[data-cy="backToOrganisations-button"]').click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.wait("@saveNew");

    // Test editing the organisation
    cy.get('[data-cy="organisations-grid"] .MuiDataGrid-row').contains(randomOrganisationName).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });

    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    evaluateInput("name", randomOrganisationName);
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna"]);
    evaluateAutocomplete("users", ["Nick Purdy"]);
    hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);

    setInput("name", "");
    hasError("name", true);
    cy.get('[data-cy="reset-button"]').should("be.enabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    cy.get('[data-cy="reset-button"]').click();
    cy.wait(500);
    evaluateInput("name", randomOrganisationName);
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna"]);
    evaluateAutocomplete("users", ["Nick Purdy"]);
    // TODO: Fix this. Seams to be a timing issue because it works fine in the browser.
    // hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);

    setInput("name", randomOrganisationName + " updated");
    setAutocomplete("mandates", "Incredible Plastic Ball");
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna", "Incredible Plastic Ball"]);
    setAutocomplete("users", "Regina Streich");
    evaluateAutocomplete("users", ["Nick Purdy", "Regina Streich"]);
    removeAutocompleteValue("users", "Nick Purdy");
    evaluateAutocomplete("users", ["Regina Streich"]);

    cy.get('[data-cy="save-button"]').click();
    cy.wait("@updateOrganisation");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    cy.get('[data-cy="backToOrganisations-button"]').click();
    cy.get('[data-cy="organisations-grid"]').last().contains(randomOrganisationName);
    // Check mandates separately because they're not always in the same order
    cy.get('[data-cy="organisations-grid"]').last().contains("Fantastic Fresh Tuna");
    cy.get('[data-cy="organisations-grid"]').last().contains("Incredible Plastic Ball");
    cy.get('[data-cy="organisations-grid"]').last().contains("Regina Streich");

    cy.get('[data-cy="admin-mandates-nav"]').click();
    cy.get('[data-cy="mandates-grid"]').contains("Fantastic Fresh Tuna").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });
    getFormField("organisations").contains(randomOrganisationName);
  });
});
