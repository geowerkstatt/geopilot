import {
  isSelectedNavItem,
  loginAsAdmin,
  loginAsNewUser,
  loginAsUploader,
  logout,
  openTool,
  selectLanguage,
} from "./helpers/appHelpers.js";
import { selectAdminNavItem } from "./helpers/adminHelpers.js";

describe("General app tests", () => {
  it("shows no login button if auth settings could not be loaded", () => {
    cy.visit("/");
    cy.intercept("/api/v1/user/auth", {
      statusCode: 200,
      body: {},
    });
    cy.get('[data-cy="loggedInUser-button"]').should("not.exist");
  });

  it("registers new users and logs them in", () => {
    loginAsNewUser();
    cy.get('[data-cy="loggedInUser-button"]').should("exist");
    cy.get('[data-cy="loggedInUser-button"]').click();
    cy.contains("Norbert Newuser");
  });

  it("shows admin tools only for admin users", () => {
    loginAsUploader();
    cy.get('[data-cy="loggedInUser-button"]').click();
    cy.get('[data-cy="delivery-nav"]').should("exist");
    cy.get('[data-cy="admin-nav"]').should("not.exist");
    cy.get('[data-cy="stacBrowser-nav"]').should("not.exist");
    logout();

    loginAsAdmin();
    cy.get('[data-cy="loggedInUser-button"]').click();
    cy.get('[data-cy="delivery-nav"]').should("exist");
    isSelectedNavItem("delivery-nav", "tool-navigation");
    cy.get('[data-cy="admin-nav"]').should("exist");
    cy.get('[data-cy="stacBrowser-nav"]').should("exist");

    openTool("admin");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/delivery-overview`);
    });
    isSelectedNavItem("admin-delivery-overview-nav", "admin-navigation");

    cy.get('[data-cy="loggedInUser-button"]').click();
    isSelectedNavItem("admin-nav", "tool-navigation");
    cy.get('[data-cy="admin-nav"]').click();

    selectAdminNavItem("users");
    selectAdminNavItem("mandates");
    selectAdminNavItem("organisations");
    selectAdminNavItem("delivery-overview");
  });

  it("updates the language when the user selects a different language", () => {
    cy.visit("/");
    cy.contains("EN");
    cy.contains("Log In");

    selectLanguage("de");
    cy.contains("DE");
    cy.contains("Anmelden");

    selectLanguage("fr");
    cy.contains("FR");
    cy.contains("Se connecter");

    selectLanguage("it");
    cy.contains("IT");
    cy.contains("Accedi");
  });
});
