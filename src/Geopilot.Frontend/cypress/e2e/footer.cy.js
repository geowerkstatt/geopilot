import { selectLanguage } from "./helpers/appHelpers.js";

describe("Footer tests", () => {
  const checkMarkdownLoading = (pagePath, markdownName, language) => {
    // Log test start with identifiable information
    cy.log(`======= STARTING TEST =======`);
    cy.log(`Page: ${pagePath}, File: ${markdownName}, Language: ${language}`);

    // Intercept ALL markdown requests to see what's happening
    cy.intercept("**/*.md*").as("allMarkdowns");

    // Specific intercepts for the test
    cy.intercept(`**/${markdownName}.${language}.md`).as("localizedMd");
    cy.intercept(`**/${markdownName}.md`).as("fallbackMd");

    // Log navigation start
    cy.log(`Navigating to ${pagePath}`);
    cy.visit(pagePath);
    cy.log(`Page loaded, waiting 2s`);
    cy.wait(2000);

    // Log language selection
    cy.log(`Selecting language: ${language}`);

    // Verbose logging for language selector
    cy.get("[data-cy=language-selector]").then($selector => {
      cy.log(`Language selector found: ${$selector.length > 0}`);
      cy.log(`Language selector text: ${$selector.text()}`);
    });

    selectLanguage(language);
    cy.log(`Language selected, waiting 1s`);
    cy.wait(1000);

    // Log ALL intercepted markdown requests for debugging
    cy.get("@allMarkdowns.all", { timeout: 5000 }).then(allRequests => {
      cy.log(`======= ALL MARKDOWN REQUESTS (${allRequests.length}) =======`);
      allRequests.forEach((req, i) => {
        const status = req.response ? req.response.statusCode : "No response";
        cy.log(`[${i + 1}] URL: ${req.request.url}, Status: ${status}`);
      });
    });

    // Check for our specific localized markdown
    cy.log(`Checking for localized markdown: ${markdownName}.${language}.md`);
    cy.get("@localizedMd.all", { timeout: 5000 }).then(interceptions => {
      cy.log(`Localized intercepts: ${interceptions.length}`);

      // Log details of each localized intercept
      interceptions.forEach((intercept, i) => {
        cy.log(`Localized intercept ${i + 1}:`);
        cy.log(`URL: ${intercept.request.url}`);
        cy.log(`Method: ${intercept.request.method}`);
        cy.log(`Has response: ${!!intercept.response}`);

        if (intercept.response) {
          cy.log(`Status: ${intercept.response.statusCode}`);
          cy.log(`Headers: ${JSON.stringify(intercept.response.headers)}`);
        }
      });

      // First check if localized version was loaded
      const localizedLoaded = interceptions.some(i => i.response && i.response.statusCode === 200);

      if (localizedLoaded) {
        cy.log(`✅ Successfully loaded localized: ${markdownName}.${language}.md`);
        expect(localizedLoaded).to.be.true;
      } else {
        cy.log(`❌ Localized version not loaded, checking fallback`);

        // Check for fallback markdown
        cy.get("@fallbackMd.all", { timeout: 5000 }).then(fallbackInterceptions => {
          cy.log(`Fallback intercepts: ${fallbackInterceptions.length}`);

          // Log details of each fallback intercept
          fallbackInterceptions.forEach((intercept, i) => {
            cy.log(`Fallback intercept ${i + 1}:`);
            cy.log(`URL: ${intercept.request.url}`);
            cy.log(`Method: ${intercept.request.method}`);
            cy.log(`Has response: ${!!intercept.response}`);

            if (intercept.response) {
              cy.log(`Status: ${intercept.response.statusCode}`);
              cy.log(`Headers: ${JSON.stringify(intercept.response.headers)}`);
            }
          });

          const fallbackLoaded = fallbackInterceptions.some(i => i.response && i.response.statusCode === 200);

          cy.log(
            fallbackLoaded
              ? `✅ Successfully loaded fallback: ${markdownName}.md`
              : `❌ FAILED: No markdown loaded for ${markdownName}`,
          );

          // Also check if any content rendered on the page
          cy.log(`Checking for visible markdown content on page`);
          cy.get("body").then($body => {
            // Look for common markdown rendering elements
            const hasContent = $body.find(".markdown-content, article, [data-markdown], .md-content").length > 0;
            cy.log(`Page has markdown content elements: ${hasContent}`);
            cy.log(`Page text length: ${$body.text().length}`);
            cy.log(`Page HTML snippet: ${$body.html().substring(0, 200)}...`);
          });

          expect(
            fallbackLoaded,
            `Either ${markdownName}.${language}.md or ${markdownName}.md should load with 200 status`,
          ).to.be.true;
        });
      }
    });

    cy.log(`======= TEST COMPLETED =======`);
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
