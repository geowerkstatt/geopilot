import { selectLanguage } from "./helpers/appHelpers.js";

describe("Footer tests", () => {
  const checkMarkdownLoading = (pagePath, markdownName, language) => {
    let debugInfo = {
      page: pagePath,
      file: markdownName,
      language: language,
      allMarkdownRequests: [],
      localizedRequests: [],
      fallbackRequests: [],
      languageSelectorInfo: "Not collected yet",
      pageContent: "Not collected yet",
    };

    // Intercept ALL markdown requests
    cy.intercept("**/*.md*").as("allMarkdowns");

    // Specific intercepts for the test
    cy.intercept(`**/${markdownName}.${language}.md`).as("localizedMd");
    cy.intercept(`**/${markdownName}.md`).as("fallbackMd");

    // Visit page
    cy.visit(pagePath);
    cy.wait(2000);

    // Collect language selector info
    cy.get("[data-cy=language-selector]").then($selector => {
      debugInfo.languageSelectorInfo = {
        exists: $selector.length > 0,
        text: $selector.text(),
      };
    });

    // Select language
    selectLanguage(language);
    cy.wait(2000);

    // Collect page content info to include in the debug
    cy.get("body").then($body => {
      const contentElements = $body.find(".markdown-content, article, [data-markdown], .md-content");
      debugInfo.pageContent = {
        hasContentElements: contentElements.length > 0,
        contentElementCount: contentElements.length,
        pageTextLength: $body.text().length,
        pageHtmlSnippet: $body.html().substring(0, 200) + "...",
      };
    });

    // Collect ALL markdown requests
    cy.get("@allMarkdowns.all", { timeout: 5000 }).then(allRequests => {
      allRequests.forEach((req, i) => {
        const status = req.response ? req.response.statusCode : "No response";
        debugInfo.allMarkdownRequests.push({
          index: i + 1,
          url: req.request.url,
          status: status,
        });
      });
    });

    // Collect localized markdown requests
    cy.get("@localizedMd.all", { timeout: 5000 }).then(interceptions => {
      interceptions.forEach((intercept, i) => {
        debugInfo.localizedRequests.push({
          index: i + 1,
          url: intercept.request.url,
          method: intercept.request.method,
          hasResponse: !!intercept.response,
          status: intercept.response ? intercept.response.statusCode : "N/A",
        });
      });

      // Check if localized version was loaded
      const localizedLoaded = interceptions.some(i => i.response && i.response.statusCode === 200);

      if (localizedLoaded) {
        expect(localizedLoaded).to.be.true;
      } else {
        // Collect fallback markdown requests
        cy.get("@fallbackMd.all", { timeout: 5000 }).then(fallbackInterceptions => {
          fallbackInterceptions.forEach((intercept, i) => {
            debugInfo.fallbackRequests.push({
              index: i + 1,
              url: intercept.request.url,
              method: intercept.request.method,
              hasResponse: !!intercept.response,
              status: intercept.response ? intercept.response.statusCode : "N/A",
            });
          });

          const fallbackLoaded = fallbackInterceptions.some(i => i.response && i.response.statusCode === 200);

          // Include ALL the debug info in the assertion message
          const detailedMessage = `
DETAILED DEBUG INFO for ${markdownName}.${language}.md:
-------------------------------------------------
Page: ${debugInfo.page}
File: ${debugInfo.file}
Language: ${debugInfo.language}

Language Selector: ${JSON.stringify(debugInfo.languageSelectorInfo)}

ALL MD Requests (${debugInfo.allMarkdownRequests.length}): 
${JSON.stringify(debugInfo.allMarkdownRequests, null, 2)}

Localized Requests (${debugInfo.localizedRequests.length}): 
${JSON.stringify(debugInfo.localizedRequests, null, 2)}

Fallback Requests (${debugInfo.fallbackRequests.length}): 
${JSON.stringify(debugInfo.fallbackRequests, null, 2)}

Page Content Info: 
${JSON.stringify(debugInfo.pageContent, null, 2)}
-------------------------------------------------

Either ${markdownName}.${language}.md or ${markdownName}.md should load with 200 status
`;

          expect(fallbackLoaded, detailedMessage).to.be.true;
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
