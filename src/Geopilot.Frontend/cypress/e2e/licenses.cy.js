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

    // Verify custom licenses are rendered before interaction
    cy.contains(".MuiAccordion-root", "project1").should("be.visible");

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

    // Verify custom licenses are rendered before interaction
    cy.contains(".MuiAccordion-root", "project1").should("be.visible");

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

    // Check that packages are grouped properly: every package in a group must belong to
    // that group's publisher/namespace. Re-query each accordion by index instead of
    // caching a jQuery element, because expanding a group reflows the list (margins and
    // borders change on expand) and would detach a cached reference mid-chain.
    cy.get(".MuiAccordion-root")
      .its("length")
      .then(groupCount => {
        for (let index = 0; index < groupCount; index++) {
          cy.get(".MuiAccordion-root").eq(index).as("group");

          cy.get("@group")
            .find("h4")
            .invoke("text")
            .then(groupName => {
              cy.get("@group").find(".MuiAccordionSummary-root").click();

              cy.get("@group")
                .find("h5")
                .each($pkgName => {
                  const packageFullName = $pkgName.text().split(" ")[0]; // name without version
                  expect(
                    packageFullName.startsWith(groupName) ||
                      groupName === "projectA" || // fixture-specific groups
                      groupName === "projectB",
                  ).to.be.true;
                });
            });
        }
      });
  });

  it("should navigate back when back button is clicked", () => {
    // Navigate to licenses page
    cy.get("#licenses-text").find("a").click();

    // Click back button using id selector
    cy.get("#backButton").click();

    // Verify we went back to about page
    cy.url().should("include", "/about");
  });
});
