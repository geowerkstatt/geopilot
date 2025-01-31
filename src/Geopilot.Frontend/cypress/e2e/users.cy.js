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
    cy.dataCy("users-grid").should("exist");
    cy.dataCy("users-grid").find(".MuiDataGrid-row").should("have.length", 10);
    cy.dataCy("users-grid").find(".MuiDataGrid-row").first().contains("Jaime Pagac");
    cy.dataCy("users-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").click();
    cy.dataCy("users-grid").find(".MuiDataGrid-row").first().contains("Andreas Admin");
  });

  it("checks for unsaved changes when navigating and allows editing users", () => {
    cy.dataCy("users-grid").find(".MuiDataGrid-row").last().contains("Bobbie Waelchi");
    cy.dataCy("users-grid")
      .find(".MuiDataGrid-row")
      .last()
      .find('[data-field="isAdmin"] [data-value="false"]')
      .should("exist");
    cy.dataCy("users-grid").find(".MuiDataGrid-row").contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    cy.dataCy("backToUsers-button").should("exist");
    cy.dataCy("reset-button").should("exist");
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("exist");
    cy.dataCy("save-button").should("be.disabled");

    isDisabled("fullName", true);
    isDisabled("email", true);
    isCheckboxDisabled("isAdmin", false);
    isDisabled("organisation", false);
    evaluateCheckbox("isAdmin", false);

    cy.dataCy("backToUsers-button").click();
    isPromptVisible(false);

    cy.dataCy("users-grid").find(".MuiDataGrid-row").contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });

    setAutocomplete("organisations", "Brown and Sons");
    cy.wait(500);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("admin-users-nav").click();
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");

    cy.dataCy("users-grid").find(".MuiDataGrid-row").last().contains("Brown and Sons").should("not.exist");
    cy.dataCy("users-grid").find(".MuiDataGrid-row").last().contains("Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    setAutocomplete("organisations", "Brown and Sons");
    toggleCheckbox("isAdmin");
    cy.dataCy("save-button").click();
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");
    cy.dataCy("backToUsers-button").click();
    isPromptVisible(false);
    cy.dataCy("users-grid").find(".MuiDataGrid-row").last().contains("Brown and Sons");
    cy.dataCy("users-grid")
      .find(".MuiDataGrid-row")
      .last()
      .find('[data-field="isAdmin"] [data-value="true"]')
      .should("exist");
    cy.dataCy("admin-organisations-nav").click();
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").contains("Brown and Sons").click();
    getFormField("users").contains("Bobbie Waelchi");
  });

  it("cannot change admin state for own user", () => {
    cy.dataCy("users-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").click();
    cy.dataCy("users-grid").find(".MuiDataGrid-row").first().contains("Andreas Admin").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    isDisabled("fullName", true);
    isDisabled("email", true);
    isCheckboxDisabled("isAdmin", true);
    isDisabled("organisation", false);
  });
});
