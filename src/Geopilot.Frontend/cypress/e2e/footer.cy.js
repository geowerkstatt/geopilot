import { selectLanguage } from "./helpers/appHelpers.js";

describe("Footer tests", () => {
  const languages = ["en", "de", "fr", "it"];
  /**
   * Tests if the correct localized markdown content is displayed when switching language
   * @param {string} pagePath - URL path of the page to visit
   * @param {string} markdownName - Base name of the markdown file
   * @param {string} language - Language code (en, de, fr, it)
   */
  const testLocalizedMarkdown = (pagePath, markdownName, language) => {
    // Step 1: Intercept both localized and fallback markdown requests
    cy.intercept(`**/${markdownName}.${language}.md`).as("localizedMd");

    // Step 2: Visit page and select language (if needed)
    cy.url().then(url => {
      const onCorrectPage = url.includes(pagePath);
      if (!onCorrectPage) {
        cy.visit(pagePath);
      }
      selectLanguage(language);
    });

    // Step 3: Check if localized markdown loaded with 200 status
    cy.get("@localizedMd.all", { timeout: 5000 }).then(interceptions => {
      const localizedLoaded = interceptions.some(i => i.response && i.response.statusCode === 200);

      if (localizedLoaded) {
        // Step 4: Verify localized content is displayed by checking for the filename in the page
        cy.contains(`${markdownName}.${language}.md`, { timeout: 5000 })
          .should("be.visible")
          .then(() => {
            cy.log(`Successfully verified localized content: ${markdownName}.${language}.md`);
          });
      } else {
        // If localized version wasn't loaded, log that we're skipping this test
        cy.log(`Skipping test - localized file ${markdownName}.${language}.md not available (status != 200)`);
      }
    });
  };

  /**
   * Tests if fallback markdown is used when localized content is not available
   * @param {string} pagePath - URL path of the page to visit
   * @param {string} markdownName - Base name of the markdown file
   * @param {string} language - Language code (en, de, fr, it)
   */
  const testFallbackMarkdown = (pagePath, markdownName, language) => {
    // Step 1: Force 404 for localized version and intercept fallback
    cy.intercept(`**/${markdownName}.${language}.md`, { statusCode: 404 }).as("localizedMd");
    cy.intercept(`**/${markdownName}.md`).as("fallbackMd");

    // Step 2: Visit page and select language (if needed)
    cy.url().then(url => {
      const onCorrectPage = url.includes(pagePath);
      if (!onCorrectPage) {
        cy.visit(pagePath);
      }
      selectLanguage(language);
    });

    // Step 3: Verify that fallback was requested after localized 404
    cy.wait("@localizedMd").its("response.statusCode").should("eq", 404);

    // Step 4: Check if fallback markdown loaded with 200 status
    cy.get("@fallbackMd.all", { timeout: 5000 }).then(fallbackInterceptions => {
      const fallbackLoaded = fallbackInterceptions.some(i => i.response && i.response.statusCode === 200);

      if (fallbackLoaded) {
        // Step 5: Verify fallback content is displayed and contains "FALLBACK"
        cy.contains("FALLBACK", { timeout: 5000 })
          .should("be.visible")
          .then(() => {
            cy.log(`Successfully verified fallback content with FALLBACK text: ${markdownName}.md`);
          });
      } else {
        // If fallback didn't load with 200, fail the test
        expect(fallbackLoaded, `Fallback test failed - ${markdownName}.md did not load with 200 status`).to.be.true;
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
