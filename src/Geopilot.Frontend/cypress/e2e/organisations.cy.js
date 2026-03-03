import { isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";
import {
  evaluateAutocomplete,
  evaluateInput,
  getFormField,
  hasError,
  removeAutocompleteValue,
  setNonFreeSoloAutocomplete,
  setInput,
} from "./helpers/formHelpers.js";

const getRandomOrganisationName = () => `Organisation-${Math.random().toString(36).substring(2, 15)}`;

describe("Organisations tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/organisations");
    isSelectedNavItem("admin-organisations-nav", "admin-navigation");
  });

  it("displays the organisations in a list with pagination", () => {
    cy.dataCy("organisations-grid").should("exist");
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").should("have.length", 3);
    cy.dataCy("organisations-grid")
      .find(".MuiTablePagination-actions [aria-label='Go to previous page']")
      .should("be.disabled");
    cy.dataCy("organisations-grid")
      .find(".MuiTablePagination-actions [aria-label='Go to next page']")
      .should("be.disabled");
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").first().contains("Schumm, Runte and Macejkovic");
  });

  it("checks for unsaved changes when navigating", () => {
    const randomOrganisationName = getRandomOrganisationName();

    cy.dataCy("addOrganisation-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    cy.dataCy("backToOrganisations-button").should("exist");
    cy.dataCy("reset-button").should("exist");
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("exist");
    cy.dataCy("save-button").should("be.disabled");

    cy.dataCy("backToOrganisations-button").click();
    isPromptVisible(false);
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations`);
    });
    cy.dataCy("organisations-grid").should("exist");
    cy.dataCy("addOrganisation-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    setNonFreeSoloAutocomplete("users", "Kelvin Spencer");
    cy.wait(500);
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("admin-users-nav").click();
    checkPromptActions(["cancel", "reset"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "cancel");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    cy.dataCy("admin-organisations-nav").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations`);
    });
    cy.dataCy("organisations-grid").should("exist");
    cy.dataCy("addOrganisation-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });
    setInput("name", randomOrganisationName);
    cy.dataCy("save-button").should("be.enabled");
    openTool("delivery");
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/`);
    });
    cy.visit("/admin/organisations");
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").last().contains(randomOrganisationName);
  });

  it("can create organisation", () => {
    const randomOrganisationName = getRandomOrganisationName();
    cy.intercept({ url: "/api/v1/organisation", method: "POST" }).as("saveNew");

    cy.dataCy("addOrganisation-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/organisations/0`);
    });

    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);

    setNonFreeSoloAutocomplete("users", "Kelvin Spencer");
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    cy.dataCy("save-button").click();
    hasError("name", true);
    cy.dataCy("save-button").should("be.disabled");

    setInput("name", randomOrganisationName);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");
    setInput("name", "");
    hasError("name", true);
    cy.dataCy("save-button").should("be.disabled");
    // We have to click away to move the focus away from the error field. Else the error won't be cleared.
    cy.contains("Description").click();

    cy.dataCy("reset-button").click();
    hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);
    evaluateInput("name", "");
    evaluateAutocomplete("mandates", []);
    evaluateAutocomplete("users", []);
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    setInput("name", randomOrganisationName);
    setNonFreeSoloAutocomplete("mandates", "Fantastic Fresh Tuna");
    setNonFreeSoloAutocomplete("users", "Nick Purdy");

    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("save-button").click();
    cy.wait("@saveNew");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    setNonFreeSoloAutocomplete("users", "Kelvin Spencer");
    cy.wait(500);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("backToOrganisations-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.dataCy("organisations-grid").last().contains(randomOrganisationName);
    cy.dataCy("organisations-grid").last().contains("Fantastic Fresh Tuna");
    cy.dataCy("organisations-grid").last().contains("Nick Purdy");
    cy.dataCy("organisations-grid").last().contains("Kevin Spencer").should("not.exist");
  });

  it("can edit existing organisation", () => {
    const randomOrganisationName = getRandomOrganisationName();
    cy.intercept({ url: "/api/v1/organisation", method: "POST" }).as("saveNew");
    cy.intercept({ url: "/api/v1/organisation", method: "PUT" }).as("updateOrganisation");

    // Create new organisation for testing
    cy.dataCy("addOrganisation-button").click();
    setInput("name", randomOrganisationName);
    setNonFreeSoloAutocomplete("mandates", "Fantastic Fresh Tuna");
    setNonFreeSoloAutocomplete("users", "Nick Purdy");
    cy.dataCy("backToOrganisations-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.wait("@saveNew");

    // Test editing the organisation
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").contains(randomOrganisationName).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });

    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    evaluateInput("name", randomOrganisationName);
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna"]);
    evaluateAutocomplete("users", ["Nick Purdy"]);
    hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);

    setInput("name", "");
    hasError("name", true);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.disabled");

    cy.dataCy("reset-button").click();
    cy.wait(500);
    evaluateInput("name", randomOrganisationName);
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna"]);
    evaluateAutocomplete("users", ["Nick Purdy"]);
    hasError("name", false);
    hasError("mandates", false);
    hasError("users", false);
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    setInput("name", randomOrganisationName + " updated");
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");
    setNonFreeSoloAutocomplete("mandates", "Incredible Plastic Ball");
    evaluateAutocomplete("mandates", ["Fantastic Fresh Tuna", "Incredible Plastic Ball"]);
    setNonFreeSoloAutocomplete("users", "Regina Streich");
    evaluateAutocomplete("users", ["Nick Purdy", "Regina Streich"]);
    removeAutocompleteValue("users", "Nick Purdy");
    evaluateAutocomplete("users", ["Regina Streich"]);

    cy.dataCy("save-button").click();
    cy.wait("@updateOrganisation");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    cy.dataCy("backToOrganisations-button").click();
    cy.dataCy("organisations-grid").last().contains(randomOrganisationName);
    // Check mandates separately because they're not always in the same order
    cy.dataCy("organisations-grid").last().contains("Fantastic Fresh Tuna");
    cy.dataCy("organisations-grid").last().contains("Incredible Plastic Ball");
    cy.dataCy("organisations-grid").last().contains("Regina Streich");

    cy.dataCy("admin-mandates-nav").click();
    cy.dataCy("mandates-grid").contains("Fantastic Fresh Tuna").click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });
    getFormField("organisations").contains(randomOrganisationName);
  });

  it("should maintain chip values in autocomplete fields after reset", () => {
    // Navigate to an existing organization
    cy.dataCy("organisations-grid").find(".MuiDataGrid-row").first().click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/organisations\/[1-9]\d*/);
    });

    // Record initial chip values before any changes
    let initialChips = [];

    // Capture all existing user chips before changes
    cy.dataCy("users-formAutocomplete")
      .find(".MuiChip-label")
      .each($chip => {
        cy.wrap($chip)
          .invoke("text")
          .then(text => {
            initialChips.push({ field: "users", value: text });
          });
      });

    // Capture all existing mandate chips before changes
    cy.dataCy("mandates-formAutocomplete")
      .find(".MuiChip-label")
      .each($chip => {
        cy.wrap($chip)
          .invoke("text")
          .then(text => {
            initialChips.push({ field: "mandates", value: text });
          });
      });

    // Add a new user to trigger a change
    cy.dataCy("users-formAutocomplete").click();
    cy.get(".MuiAutocomplete-popper").should("be.visible");

    // Find and click on "Jaime Pagac" user in dropdown
    cy.get(".MuiAutocomplete-option").contains("Jaime Pagac").click();

    // Verify reset button is enabled after changes
    cy.dataCy("reset-button").should("be.enabled");

    // Click reset button
    cy.dataCy("reset-button").click();
    cy.wait(500); // Wait for reset to complete

    // Verify all initial chips are still present with correct values
    cy.wrap(initialChips).each(chip => {
      cy.dataCy(`${chip.field}-formAutocomplete`).find(".MuiChip-label").contains(chip.value).should("exist");
    });

    // Verify Jaime Pagac is not present in the users chips after reset
    cy.dataCy("users-formAutocomplete").find(".MuiChip-label").contains("Jaime Pagac").should("not.exist");

    // Verify no empty string chips exist (the key bug you're testing for)
    cy.dataCy("users-formAutocomplete")
      .find(".MuiChip-label")
      .each($chip => {
        cy.wrap($chip).invoke("text").should("not.be.empty");
      });

    cy.dataCy("mandates-formAutocomplete")
      .find(".MuiChip-label")
      .each($chip => {
        cy.wrap($chip).invoke("text").should("not.be.empty");
      });
  });

  it("should not duplicate autocomplete options when typing and clearing", () => {
    // Intercept the mandates API call
    cy.intercept("GET", "**/api/v1/mandate*", req => {
      req.continue(res => {
        // Make a copy of the first mandate and add it to the response
        if (res.body && Array.isArray(res.body)) {
          const firstMandate = { ...res.body[0] };
          res.body.push(firstMandate);
        } else if (res.body && res.body.data && Array.isArray(res.body.data)) {
          const firstMandate = { ...res.body.data[0] };
          res.body.data.push(firstMandate);
        }
        console.log("Modified mandate response:", res.body);
      });
    }).as("mandatesRequest");

    // Intercept the users API call
    cy.intercept("GET", "**/api/v1/user*", req => {
      req.continue(res => {
        // Make a copy of the first user and add it to the response
        if (res.body && Array.isArray(res.body)) {
          const firstUser = { ...res.body[0] };
          res.body.push(firstUser);
        } else if (res.body && res.body.data && Array.isArray(res.body.data)) {
          const firstUser = { ...res.body.data[0] };
          res.body.data.push(firstUser);
        }
        console.log("Modified user response:", res.body);
      });
    }).as("usersRequest");

    // Visit the organisations page
    cy.visit("/admin/organisations");

    // Create a new organisation
    cy.dataCy("addOrganisation-button").click();
    setInput("name", getRandomOrganisationName());

    // Test mandates autocomplete
    cy.log("Testing mandates autocomplete");
    testAutocomplete("mandates-formAutocomplete");

    // Test users autocomplete
    cy.log("Testing users autocomplete");
    testAutocomplete("users-formAutocomplete");

    // Function to test an autocomplete field
    function testAutocomplete(autocompleteSelector) {
      // Click on the autocomplete field
      cy.dataCy(autocompleteSelector).click();

      // Wait for the dropdown to appear
      cy.get(".MuiAutocomplete-popper").should("be.visible");

      // Get the initial count of autocomplete options
      let initialOptionCount;
      let itemToSearch;

      cy.get(".MuiAutocomplete-popper .MuiAutocomplete-option")
        .its("length")
        .then(count => {
          initialOptionCount = count;
          cy.log(`Initial ${autocompleteSelector} options count: ${initialOptionCount}`);

          // Get the text of a non-duplicate option to search for (use the second item)
          cy.get(".MuiAutocomplete-popper .MuiAutocomplete-option")
            .eq(1)
            .invoke("text")
            .then(text => {
              itemToSearch = text;
              cy.log(`Will search for item: ${itemToSearch}`);

              // Function to perform type-delete cycle and verify count
              const performTypingCycle = (cycleNumber, searchText) => {
                // Check if dropdown is visible
                cy.get("body").then($body => {
                  const isDropdownVisible = $body.find(".MuiAutocomplete-popper").length > 0;

                  // If dropdown is not visible, click to open it
                  if (!isDropdownVisible) {
                    cy.dataCy(autocompleteSelector).click();
                  }

                  // Now we can be sure the dropdown is visible
                  cy.get(".MuiAutocomplete-popper").should("be.visible");

                  // Type the search text
                  cy.dataCy(autocompleteSelector).find("input").type(searchText);
                  cy.wait(500); // Wait for filtering

                  // Delete the text
                  cy.dataCy(autocompleteSelector).find("input").clear();
                  cy.wait(500); // Wait for options to reset

                  // Check if the number of options remains the same
                  cy.get(".MuiAutocomplete-popper .MuiAutocomplete-option")
                    .its("length")
                    .then(newCount => {
                      // Log the counts
                      cy.log(`Cycle ${cycleNumber}: Option count is ${newCount}, should be ${initialOptionCount}`);
                      // Assert count is the same
                      expect(newCount).to.equal(initialOptionCount);
                    });

                  // Close the dropdown
                  cy.get("body").click({ force: true });
                });
              };

              // Perform cycles with matching text (first 4 chars of a real item)
              performTypingCycle(1, itemToSearch.substring(0, 4));
              performTypingCycle(2, itemToSearch.substring(0, 4));

              // Perform cycles with non-matching text
              performTypingCycle(3, "XYZ123");
              performTypingCycle(4, "ZZZ999");
            });
        });
    }
  });
});
