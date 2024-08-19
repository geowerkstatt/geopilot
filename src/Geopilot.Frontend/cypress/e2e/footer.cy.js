describe("Footer tests", () => {
  it("shows and navigates correctly between footer pages with content", () => {
    cy.intercept("privacy-policy.md", {
      statusCode: 200,
      fixture: "../fixtures/privacy-policy.md",
    }).as("privacyPolicy");
    cy.intercept("imprint.md", {
      statusCode: 200,
      fixture: "../fixtures/imprint.md",
    }).as("imprint");
    cy.intercept("terms-of-use.md", {
      statusCode: 200,
      fixture: "../fixtures/terms-of-use.md",
    }).as("termsOfUse");
    cy.intercept("info.md", {
      statusCode: 200,
      fixture: "../fixtures/info.md",
    }).as("info");
    cy.intercept("licenses.json", {
      statusCode: 200,
      fixture: "../fixtures/licenses.json",
    }).as("license");
    cy.intercept("licenses.custom.json", {
      statusCode: 200,
      fixture: "../fixtures/licenses.custom.json",
    }).as("licenseCustom");

    cy.visit("/");

    cy.get('[data-cy="privacy-policy-nav"]').click();
    cy.wait("@privacyPolicy");
    cy.contains("Your privacy is important to us");

    cy.get('[data-cy="imprint-nav"]').click();
    cy.wait("@imprint");
    cy.contains("Test imprint");

    cy.get('[data-cy="about-nav"]').click();
    cy.wait("@info");
    cy.wait("@termsOfUse");
    cy.wait("@license");
    cy.wait("@licenseCustom");
    const expectedHeaders = [
      "Information about geopilot",
      "Terms of use",
      "Version information",
      "Development & bug tracking",
      "License information",
    ];
    cy.get("h1")
      .should("have.length", expectedHeaders.length)
      .each(($el, index) => {
        cy.wrap($el).should("contain.text", expectedHeaders[index]);
      });
    cy.contains("ili2db");
    cy.contains("msal-browser");
  });

  it("shows and navigates correctly between footer pages without content", () => {
    cy.intercept("privacy-policy.md", {
      statusCode: 500,
    }).as("privacyPolicy");
    cy.intercept("imprint.md", {
      statusCode: 500,
    }).as("imprint");
    cy.intercept("terms-of-use.md", {
      statusCode: 500,
    }).as("termsOfUse");
    cy.intercept("info.md", {
      statusCode: 500,
    }).as("info");
    cy.intercept("licenses.json", {
      statusCode: 500,
    }).as("license");
    cy.intercept("licenses.custom.json", {
      statusCode: 500,
    }).as("licenseCustom");

    cy.visit("/");

    cy.get('[data-cy="privacy-policy-nav"]').click();
    cy.wait("@privacyPolicy");
    cy.contains("Oops, nothing found!");

    cy.get('[data-cy="imprint-nav"]').click();
    cy.wait("@imprint");
    cy.contains("Oops, nothing found!");

    cy.get('[data-cy="about-nav"]').click();
    cy.wait("@info");
    cy.wait("@termsOfUse");
    cy.wait("@license");
    cy.wait("@licenseCustom");
    const expectedHeaders = ["Version information", "Development & bug tracking"];
    cy.get("h1")
      .should("have.length", expectedHeaders.length)
      .each(($el, index) => {
        cy.wrap($el).should("contain.text", expectedHeaders[index]);
      });
  });
});
