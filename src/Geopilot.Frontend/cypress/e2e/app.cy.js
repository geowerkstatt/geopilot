import {
  isSelectedNavItem,
  loadWithoutAuth,
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
    loadWithoutAuth();
    cy.dataCy("login-button").should("not.exist");
    cy.dataCy("delivery").should("exist");
  });

  it.skip("registers new users and logs them in", () => {
    loginAsNewUser();
    cy.dataCy("loggedInUser-button").should("exist");
    cy.dataCy("loggedInUser-button").click();
    cy.contains("Norbert Newuser");
  });

  it("shows admin tools only for admin users", () => {
    loginAsUploader();
    cy.dataCy("loggedInUser-button").click();
    cy.dataCy("delivery-nav").should("exist");
    cy.dataCy("admin-nav").should("not.exist");
    cy.dataCy("stacBrowser-nav").should("not.exist");
    logout();

    loginAsAdmin();
    cy.dataCy("loggedInUser-button").click();
    cy.dataCy("delivery-nav").should("exist");
    isSelectedNavItem("delivery-nav", "tool-navigation");
    cy.dataCy("admin-nav").should("exist");
    cy.dataCy("stacBrowser-nav").should("exist");

    openTool("admin");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/delivery-overview`);
    });
    isSelectedNavItem("admin-delivery-overview-nav", "admin-navigation");

    cy.dataCy("loggedInUser-button").click();
    isSelectedNavItem("admin-nav", "tool-navigation");
    cy.dataCy("admin-nav").click();

    selectAdminNavItem("users");
    selectAdminNavItem("mandates");
    selectAdminNavItem("organisations");
    selectAdminNavItem("delivery-overview");
    cy.reload();
    isSelectedNavItem("admin-delivery-overview-nav", "admin-navigation");
    cy.location().should(location => {
      expect(location.pathname).to.eq("/admin/delivery-overview");
    });
  });

  it("updates the language when the user selects a different language", () => {
    cy.visit("/");

    cy.contains("EN");
    cy.contains("Click to select");

    selectLanguage("de");
    cy.contains("Zum Auswählen klicken");

    selectLanguage("fr");
    cy.contains("Cliquer pour sélectionner");

    selectLanguage("it");
    cy.contains("Clicca per selezionare");
  });

  it("displays correct localized application name when language changes", () => {
    // Intercept the client-settings.json request to dynamically extract the values
    cy.intercept("**/client-settings.json").as("clientSettings");

    // Visit the home page
    cy.visit("/");

    // Wait for client settings to load and extract the localNames
    cy.wait("@clientSettings").then(interception => {
      // Extract the application settings from the intercepted response
      const settings = interception.response.body;
      const localNames = settings.application.localName;

      // Wait for the language selector to load
      cy.wait(500);

      // Test each available language
      Object.entries(localNames).forEach(([language, expectedName]) => {
        // Skip languages that aren't supported in your language selector
        if (!["en", "de", "fr", "it"].includes(language)) return;

        // Switch to this language
        selectLanguage(language);

        // Verify the localized application name appears on the page
        // Note: You may need to adjust this selector to match where the app name appears
        cy.contains(expectedName).should("be.visible");

        // Log success
        cy.log(`Successfully verified ${language.toUpperCase()} localized name: ${expectedName}`);
      });
    });
  });
});
