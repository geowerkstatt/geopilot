import { selectAdminNavItem } from "./helpers/adminHelpers.js";
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

    selectLanguage("en");
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
      expect(Object.keys(localNames)).to.have.length.greaterThan(0);

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

  it("displays the configured localized delivery title when language changes", () => {
    cy.intercept("**/client-settings.json").as("clientSettings");

    cy.visit("/");

    cy.wait("@clientSettings").then(interception => {
      const localTitle = interception.response.body.application.localTitle;
      expect(Object.keys(localTitle)).to.have.length.greaterThan(0);

      Object.entries(localTitle).forEach(([language, expectedTitle]) => {
        if (!["en", "de", "fr", "it"].includes(language)) return;

        selectLanguage(language);
        cy.dataCy("delivery-title").should("be.visible").and("contain", expectedTitle);
      });
    });
  });

  it("normalises region-specific browser locales to their base language", () => {
    // With load: "languageOnly", a browser locale like de-CH resolves to de instead of falling back
    // to English. We assert the rendered title (the resolved language); the raw i18next cookie keeps
    // the region code, so it is not a reliable check here. Covered for all four supported languages.
    cy.intercept("**/client-settings.json").as("clientSettings");

    [
      { locale: "de-CH", base: "de" },
      { locale: "fr-CH", base: "fr" },
      { locale: "it-CH", base: "it" },
      { locale: "en-US", base: "en" },
    ].forEach(({ locale, base }) => {
      cy.setCookie("i18next", locale);

      cy.visit("/");

      cy.wait("@clientSettings").then(interception => {
        const expectedTitle = interception.response.body.application.localTitle[base];
        cy.dataCy("delivery-title").should("be.visible").and("contain", expectedTitle);
      });
    });
  });

  it("hides the delivery title when none is configured", () => {
    cy.intercept("**/client-settings.json", req => {
      req.continue(res => {
        const modifiedBody = { ...res.body };
        delete modifiedBody.application.localTitle;
        res.send({ body: modifiedBody });
      });
    }).as("settings");

    cy.visit("/");

    cy.wait("@settings");
    cy.dataCy("delivery").should("exist");
    cy.dataCy("delivery-title").should("not.exist");
  });

  it("falls back to another configured language for the application name", () => {
    const fallbackName = "geowerkstatt Fallback DE";

    // Configure the application name only in German, so the other languages must fall back to it.
    cy.intercept("**/client-settings.json", req => {
      req.continue(res => {
        const modifiedBody = { ...res.body };
        modifiedBody.application.localName = { de: fallbackName };
        res.send({ body: modifiedBody });
      });
    }).as("settings");

    cy.visit("/");
    cy.wait("@settings");

    ["en", "fr", "it", "de"].forEach(language => {
      selectLanguage(language);
      cy.contains(fallbackName).should("be.visible");
    });
  });
});
