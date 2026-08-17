import { getGridRowThatContains, isSelectedNavItem, loginAsAdmin } from "./helpers/appHelpers.js";
import {
  evaluateAutocomplete,
  evaluateCheckbox,
  getFormField,
  isCheckboxDisabled,
  isDisabled,
  setNonFreeSoloAutocomplete,
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
    cy.dataCy("users-grid").find(".MuiDataGrid-row").should("have.length.of.at.least", 12); //New user might be created in other tests
    cy.dataCy("users-grid").find(".MuiDataGrid-row").first().contains("Jaime Pagac");
    cy.dataCy("users-grid")
      .find(".MuiTablePagination-actions [aria-label='Go to previous page']")
      .should("be.disabled");
    cy.dataCy("users-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").should("be.disabled");
  });

  it("checks for unsaved changes when navigating and allows editing users", () => {
    getGridRowThatContains("users-grid", "Bobbie Waelchi")
      .find('[data-field="isAdmin"] [aria-label="no"]')
      .should("exist");
    getGridRowThatContains("users-grid", "Bobbie Waelchi").click();
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

    getGridRowThatContains("users-grid", "Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });

    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Brown and Sons"]);
    cy.wait(500);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("admin-users-nav").click();
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");

    getGridRowThatContains("users-grid", "Bobbie Waelchi").contains("Brown and Sons").should("not.exist");
    getGridRowThatContains("users-grid", "Bobbie Waelchi").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    toggleCheckbox("isAdmin");
    cy.dataCy("save-button").click();
    // After saving we are redirected to the list, where the saved changes are visible.
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/users`);
    });
    getGridRowThatContains("users-grid", "Bobbie Waelchi").contains("Brown and Sons");
    getGridRowThatContains("users-grid", "Bobbie Waelchi")
      .find('[data-field="isAdmin"] [aria-label="yes"]')
      .should("exist");
    cy.dataCy("admin-organisations-nav").click();
    getGridRowThatContains("organisations-grid", "Brown and Sons").click();
    getFormField("users").contains("Bobbie Waelchi");
  });

  it("cannot change admin state for own user", () => {
    getGridRowThatContains("users-grid", "Andreas Admin").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    isDisabled("fullName", true);
    isDisabled("email", true);
    isCheckboxDisabled("isAdmin", true);
    isDisabled("organisation", false);
  });

  it("keeps own admin rights and active state when saving a self-edit", () => {
    cy.intercept({ url: "/api/v1/user", method: "PUT" }).as("updateUser");

    // Open your own account. "Admin" and "Active" are disabled here, so they are not submitted.
    getGridRowThatContains("users-grid", "Andreas Admin").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/users\/(?!0\b)\d+/);
    });
    isCheckboxDisabled("isAdmin", true);
    evaluateCheckbox("isAdmin", true);

    // Change the only editable field (organisations) so the form becomes dirty and can be saved.
    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("save-button").click();

    // The request must keep the current admin/active values instead of the
    // disabled, unsubmitted fields, otherwise the admin would lock themselves out.
    cy.wait("@updateUser")
      .its("request.body")
      .should(body => {
        expect(body.isAdmin).to.eq(true);
        expect(body.state).to.eq("active");
      });

    // After the redirect to the list, the user is still an administrator.
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/users`);
    });
    getGridRowThatContains("users-grid", "Andreas Admin")
      .find('[data-field="isAdmin"] [aria-label="yes"]')
      .should("exist");
  });
});
