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
    loginAsAdmin();
    openTool("admin");

    cy.contains("EN");
    cy.contains("Rows per page");

    selectLanguage("de");
    cy.contains("DE");
    cy.contains("Zeilen pro Seite");

    selectLanguage("fr");
    cy.contains("FR");
    cy.contains("Lignes par page");

    selectLanguage("it");
    cy.contains("IT");
    cy.contains("Righe per pagina");
  });
});
