import { loginAsAdmin, loginAsEditor, selectLanguage } from "./helpers/appHelpers.js";

describe("General app tests", () => {
  it("shows no login button if auth settings could not be loaded", () => {
    cy.visit("/");
    cy.intercept("/api/v1/user/auth", {
      statusCode: 200,
      body: {},
    });
    cy.get('[data-cy="loggedInUser-button"]').should("not.exist");
  });

  it("shows admin tools only for admin users", () => {
    loginAsEditor();
    cy.get('[data-cy="loggedInUser-button"]').click();
    cy.get('[data-cy="delivery-nav"]').should("exist");
    cy.get('[data-cy="administration-nav"]').should("not.exist");
    cy.get('[data-cy="stacBrowser-nav"]').should("not.exist");
    cy.get('[data-cy="logout-button"]').click();

    loginAsAdmin();
    cy.get('[data-cy="loggedInUser-button"]').click();
    cy.get('[data-cy="delivery-nav"]').should("exist");
    cy.get('[data-cy="administration-nav"]').should("exist");
    cy.get('[data-cy="stacBrowser-nav"]').should("exist");
    cy.get('[data-cy="logout-button"]').click();
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
