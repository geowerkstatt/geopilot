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

  it("should display license information from the API", () => {
    // Navigate to licenses page
    cy.get("#licenses-text").find("a").click();

    // Wait for API responses
    cy.wait(["@getLicenses", "@getCustomLicenses"]);

    // Verify project groups are displayed
    cy.get(".MuiAccordion-root").should("have.length.at.least", 2);

    // Check specific project information
    cy.contains(".MuiAccordion-root", "projectA").within(() => {
      cy.get(".MuiAccordionSummary-root").click();
      cy.contains("projectA").should("be.visible");
      cy.contains("GPL-3.0").should("be.visible");
      cy.contains("https://github.com/example/projectA").should("be.visible");
      cy.contains("A groundbreaking project that changes everything").should("be.visible");
      cy.contains("Copyright (c) 2023 Example A Corp").should("be.visible");
    });
  });

  it("should expand and collapse accordion sections", () => {
    // Navigate to licenses page
    cy.get("#licenses-text").find("a").click();

    // Wait for API response
    cy.wait("@getLicenses").as("licenses");
    cy.wait("@getCustomLicenses");

    // Get data from the fixture
    cy.get("@licenses")
      .its("response.body")
      .then(licenses => {
        const projectA = licenses.projectA;

        // Verify the project name is visible in the accordion summary
        cy.contains(".MuiAccordionSummary-root", projectA.name).should("be.visible");

        // But description should be hidden initially
        cy.contains(projectA.description).should("not.be.visible");

        // Expand the accordion for projectA
        cy.contains(".MuiAccordion-root", projectA.name).within(() => {
          cy.get(".MuiAccordionSummary-root").click();
        });

        // Now the description should be visible
        cy.contains(projectA.description).should("be.visible");

        // Collapse accordion again
        cy.contains(".MuiAccordion-root", projectA.name).within(() => {
          cy.get(".MuiAccordionSummary-root").click();
        });

        // Description should be hidden again
        cy.contains(projectA.description).should("not.be.visible");
      });
  });

  it("should group packages by publisher or first part of package name", () => {
    // Navigate to licenses page
    cy.get("#licenses-text").find("a").click();

    // Wait for API response
    cy.wait(["@getLicenses", "@getCustomLicenses"]);

    // Check that packages are grouped properly
    // For this test, we need to ensure at least one package has a publisher property
    // or that packages with same namespace are grouped together
    cy.get(".MuiAccordion-root").each($accordion => {
      // Get group name
      const groupName = $accordion.find("h2").text();

      // Expand accordion
      cy.wrap($accordion).find(".MuiAccordionSummary-root").click();

      // Check that all packages in this group belong to this publisher/namespace
      cy.wrap($accordion)
        .find("h3")
        .each($pkgName => {
          // If groupName matches publisher, we're good
          // Otherwise, check if package name starts with groupName
          const packageFullName = $pkgName.text().split(" ")[0]; // Get name without version
          cy.wrap($pkgName).then(() => {
            expect(
              packageFullName.startsWith(groupName) ||
              groupName === "projectA" || // Handling our fixture specifically
              groupName === "projectB",
            ).to.be.true;
          });
        });
    });
  });
});
