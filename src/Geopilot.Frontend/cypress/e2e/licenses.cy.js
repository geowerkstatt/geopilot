// cypress/e2e/licenses.cy.js

describe("Licenses Component", () => {
  beforeEach(() => {
    // Intercept API calls and provide mock data
    cy.intercept("GET", "**/license.json", { fixture: "license.json" }).as("getLicenses");
    cy.intercept("GET", "**/license.custom.json", { fixture: "license.custom.json" }).as("getCustomLicenses");

    // Visit the about page first
    cy.visit("/about");
  });

  it("should navigate to the licenses page from the about page", () => {
    // Find and click the license link in the about page
    cy.get("#licenses-text").find("a").click();

    // Verify URL changed to licenses page
    cy.url().should("include", "/licenses");

    // Verify page title is displayed
    cy.get("h1").should("contain", "License information");
  });
});
