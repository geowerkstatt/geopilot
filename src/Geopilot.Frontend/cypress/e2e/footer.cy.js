import { selectLanguage } from "./helpers/appHelpers.js";

describe("Footer tests", () => {
  const checkMarkdownLoading = (pagePath, markdownName, language) => {
    // Intercept both localized and fallback markdown requests
    cy.intercept(`**/${markdownName}.${language}.md`).as("localizedMd");
    cy.intercept(`**/${markdownName}.md`).as("fallbackMd");

    cy.visit(pagePath);
    cy.wait(2000); // Longer wait after page load

    // Select language and wait again
    selectLanguage(language);
    cy.wait(1000); // Wait after language change

    // Check if either markdown loads successfully
    cy.get("@localizedMd.all", { timeout: 5000 }).then(interceptions => {
      // First check if localized version was loaded
      const localizedLoaded = interceptions.some(i => i.response && i.response.statusCode === 200);

      if (localizedLoaded) {
        cy.log(`Successfully loaded localized: ${markdownName}.${language}.md`);
        expect(localizedLoaded).to.be.true;
      } else {
        // If localized version wasn't loaded, check fallback
        cy.get("@fallbackMd.all", { timeout: 5000 }).then(fallbackInterceptions => {
          const fallbackLoaded = fallbackInterceptions.some(i => i.response && i.response.statusCode === 200);
          cy.log(
            fallbackLoaded
              ? `Successfully loaded fallback: ${markdownName}.md`
              : `Failed to load both ${markdownName}.${language}.md and ${markdownName}.md`,
          );
          expect(
            fallbackLoaded,
            `Either ${markdownName}.${language}.md or ${markdownName}.md should load with 200 status`,
          ).to.be.true;
        });
      }
    });
  };

  it("shows and navigates correctly between footer pages with content", () => {
    cy.intercept("privacy-policy*.md", {
      statusCode: 200,
      fixture: "../fixtures/privacy-policy.md",
    }).as("privacyPolicy");
    cy.intercept("imprint*.md", {
      statusCode: 200,
      fixture: "../fixtures/imprint.md",
    }).as("imprint");
    cy.intercept("terms-of-use*.md", {
      statusCode: 200,
      fixture: "../fixtures/terms-of-use.md",
    }).as("termsOfUse");
    cy.intercept("info*.md", {
      statusCode: 200,
      fixture: "../fixtures/info.md",
    }).as("info");
    cy.intercept("license.json", {
      statusCode: 200,
      fixture: "../fixtures/license.json",
    }).as("license");
    cy.intercept("license.custom.json", {
      statusCode: 200,
      fixture: "../fixtures/license.custom.json",
    }).as("licenseCustom");

    cy.visit("/");

    cy.dataCy("privacy-policy-nav").click();
    cy.wait("@privacyPolicy");
    cy.contains("Your privacy is important to us");

    cy.dataCy("imprint-nav").click();
    cy.wait("@imprint");
    cy.contains("Test imprint");

    cy.dataCy("about-nav").click();
    cy.wait("@info");
    cy.wait("@termsOfUse");
    cy.wait("@license");
    cy.wait("@licenseCustom");
    const expectedHeaders = [
      "Information about geopilot",
      "Terms of use",
      "API",
      "Development & bug tracking",
      "License information",
    ];
    cy.get("h1")
      .should("have.length", expectedHeaders.length)
      .each(($el, index) => {
        cy.wrap($el).should("contain.text", expectedHeaders[index]);
      });
    cy.contains("project1");
    cy.contains("projectA");

    cy.dataCy("header").click();
    cy.dataCy("upload-step").should("exist");
  });

  it("shows and navigates correctly between footer pages without content", () => {
    cy.intercept("privacy-policy*.md", {
      statusCode: 500,
    }).as("privacyPolicy");
    cy.intercept("imprint*.md", {
      statusCode: 500,
    }).as("imprint");
    cy.intercept("terms-of-use*.md", {
      statusCode: 500,
    }).as("termsOfUse");
    cy.intercept("info*.md", {
      statusCode: 500,
    }).as("info");
    cy.intercept("license.json", {
      statusCode: 500,
    }).as("license");
    cy.intercept("license.custom.json", {
      statusCode: 500,
    }).as("licenseCustom");

    cy.visit("/");

    cy.dataCy("privacy-policy-nav").click();
    cy.wait("@privacyPolicy");
    cy.contains("Oops, nothing found!");

    cy.dataCy("imprint-nav").click();
    cy.wait("@imprint");
    cy.contains("Oops, nothing found!");

    cy.dataCy("about-nav").click();
    cy.wait("@info");
    cy.wait("@termsOfUse");
    cy.wait("@license");
    cy.wait("@licenseCustom");
    const expectedHeaders = ["API", "Development & bug tracking"];
    cy.get("h1")
      .should("have.length", expectedHeaders.length)
      .each(($el, index) => {
        cy.wrap($el).should("contain.text", expectedHeaders[index]);
      });

    cy.reload();
    cy.location().should(location => {
      expect(location.pathname).to.eq("/about");
    });
    cy.wait("@info");
    cy.wait("@termsOfUse");
    cy.wait("@license");
    cy.wait("@licenseCustom");
    cy.get("h1")
      .should("have.length", expectedHeaders.length)
      .each(($el, index) => {
        cy.wrap($el).should("contain.text", expectedHeaders[index]);
      });
  });

  it("checks privacy policy page loads correct markdown in all languages", () => {
    const languages = ["en", "de", "fr", "it"];

    languages.forEach(language => {
      checkMarkdownLoading("/privacy-policy", "privacy-policy", language);
    });
  });

  it("checks imprint page loads correct markdown in all languages", () => {
    const languages = ["en", "de", "fr", "it"];

    languages.forEach(language => {
      checkMarkdownLoading("/imprint", "imprint", language);
    });
  });

  it("checks about page loads both required markdown files in all languages", () => {
    const languages = ["en", "de", "fr", "it"];

    languages.forEach(language => {
      // Check terms-of-use markdown (don't visit page again)
      checkMarkdownLoading("/about", "terms-of-use", language);

      // Check info markdown (don't visit page again)
      checkMarkdownLoading("/about", "info", language);
    });
  });
});
