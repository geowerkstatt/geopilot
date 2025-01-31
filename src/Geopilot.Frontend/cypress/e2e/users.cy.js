import { isSelectedNavItem, loginAsAdmin } from "./helpers/appHelpers.js";
import {
  evaluateCheckbox,
  getFormField,
  isCheckboxDisabled,
  isDisabled,
  setAutocomplete,
  toggleCheckbox,
} from "./helpers/formHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";

describe("Users tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/users");
    isSelectedNavItem("admin-users-nav", "admin-navigation");
  });

  it("displays the users in a list with pagination", () => {
    cy.get('[data-cy="users-grid"]').should("exist");
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').should("have.length", 10);
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').first().contains("Jaime Pagac");
    cy.get('[data-cy="users-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').click();
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').first().contains("Andreas Admin");
  });

  it("checks for unsaved changes when navigating and allows editing users", () => {
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').last().contains("Bobbie Waelchi");
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row')
      .last()
      .find('[data-field="isAdmin"] [data-value="false"]')
      .should("exist");
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    cy.get('[data-cy="backToUsers-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("exist");
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("exist");
    cy.get('[data-cy="save-button"]').should("be.disabled");

    isDisabled("fullName", true);
    isDisabled("email", true);
    isCheckboxDisabled("isAdmin", false);
    isDisabled("organisation", false);
    evaluateCheckbox("isAdmin", false);

    cy.get('[data-cy="backToUsers-button"]').click();
    isPromptVisible(false);

    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });

    setAutocomplete("organisations", "Brown and Sons");
    cy.wait(500);
    cy.get('[data-cy="reset-button"]').should("be.enabled");
    cy.get('[data-cy="save-button"]').should("be.enabled");
    cy.get('[data-cy="admin-users-nav"]').click();
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");

    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').last().contains("Brown and Sons").should("not.exist");
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').last().contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    setAutocomplete("organisations", "Brown and Sons");
    toggleCheckbox("isAdmin");
    cy.get('[data-cy="save-button"]').click();
    cy.get('[data-cy="reset-button"]').should("be.disabled");
    cy.get('[data-cy="save-button"]').should("be.disabled");
    cy.get('[data-cy="backToUsers-button"]').click();
    isPromptVisible(false);
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').last().contains("Brown and Sons");
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row')
      .last()
      .find('[data-field="isAdmin"] [data-value="true"]')
      .should("exist");
    cy.get('[data-cy="admin-organisations-nav"]').click();
    cy.get('[data-cy="organisations-grid"] .MuiDataGrid-row').contains("Brown and Sons").click();
    getFormField("users").contains("Bobbie Waelchi");
  });

  it("cannot change admin state for own user", () => {
    cy.get('[data-cy="users-grid"] .MuiTablePagination-actions [aria-label="Go to next page"]').click();
    cy.get('[data-cy="users-grid"] .MuiDataGrid-row').first().contains("Andreas Admin").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    isDisabled("fullName", true);
    isDisabled("email", true);
    isCheckboxDisabled("isAdmin", true);
    isDisabled("organisation", false);
  });
});
