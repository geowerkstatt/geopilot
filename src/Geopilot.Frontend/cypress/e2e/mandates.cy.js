import { isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import {
  evaluateAutocomplete,
  evaluateInput,
  evaluateSelect,
  hasError,
  setAutocomplete,
  setInput,
  setSelect,
} from "./helpers/formHelpers.js";
import { checkPromptActions, handlePrompt, isPromptVisible } from "./helpers/promptHelpers.js";

const getRandomManadateName = () => `Mandate-${Math.random().toString(36).substring(2, 15)}`;

describe("Mandate tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/mandates");
    isSelectedNavItem(`admin-mandates-nav`, "admin-navigation");
  });

  it("displays the mandates in a list with pagination", () => {
    cy.dataCy("mandates-grid").should("exist");
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").should("have.length", 10);
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").first().contains("Handmade Soft Cheese");
    cy.dataCy("mandates-grid")
      .find(".MuiTablePagination-actions [aria-label='Go to previous page']")
      .should("be.disabled");
    cy.dataCy("mandates-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").should("be.disabled");
  });

  it("checks for unsaved changes when navigating", () => {
    const randomMandateName = getRandomManadateName();

    cy.dataCy("addMandate-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    cy.dataCy("backToMandates-button").should("exist");
    cy.dataCy("reset-button").should("exist");
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("exist");
    cy.dataCy("save-button").should("be.enabled");

    cy.dataCy("backToMandates-button").click();
    isPromptVisible(false);
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates`);
    });
    cy.dataCy("mandates-grid").should("exist");

    cy.dataCy("addMandate-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    setInput("name", randomMandateName);
    cy.contains("Description").click();
    cy.wait(500); // Click outside the input field and wait to trigger the validation.
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("admin-users-nav").click();
    checkPromptActions(["cancel", "reset"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "cancel");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    cy.dataCy("admin-mandates-nav").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates`);
    });
    cy.dataCy("mandates-grid").should("exist");

    cy.dataCy("addMandate-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });
    setInput("name", randomMandateName);
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("evaluatePrecursorDelivery", 0, 3);
    setSelect("evaluatePartial", 1, 2);
    setSelect("evaluateComment", 1, 3);
    openTool("delivery");
    checkPromptActions(["cancel", "reset", "save"]);
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/`);
    });

    cy.visit("/admin/mandates");
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").last().contains(randomMandateName);
  });

  it("can create mandate", () => {
    const randomMandateName = getRandomManadateName();
    cy.intercept({ url: "/api/v1/mandate", method: "POST" }).as("saveNew");

    cy.dataCy("addMandate-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });

    // Buttons should be disabled if form is untouched.
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabeld");

    // Fields should not show errors before they are touched.
    hasError("name", false);
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);
    hasError("evaluatePrecursorDelivery", false);
    hasError("evaluatePartial", false);
    hasError("evaluateComment", false);

    // Buttons should be enabled if form is touched.
    setInput("name", randomMandateName);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    // Extent fields should show errors when one is touched.
    setInput("extent-bottom-left-longitude", "7.3");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-bottom-left-latitude", "47.13");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-upper-right-longitude", "8.052");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    setInput("extent-upper-right-latitude", "47.46");
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);

    // Save should trigger validation and show errors if fields are not valid.
    cy.dataCy("save-button").click();
    cy.dataCy("save-button").should("be.disabled");
    hasError("evaluatePrecursorDelivery", true);
    hasError("evaluatePartial", true);
    hasError("evaluateComment", true);

    setSelect("evaluatePrecursorDelivery", 0, 3);
    hasError("evaluatePrecursorDelivery", false);
    setSelect("evaluatePartial", 1, 2);
    hasError("evaluatePartial", false);
    setSelect("evaluateComment", 1, 3);
    hasError("evaluateComment", false);
    cy.dataCy("save-button").should("be.enabled");

    setAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Brown and Sons"]);
    setAutocomplete("fileTypes", ".csv");
    setAutocomplete("fileTypes", ".xtf");
    evaluateAutocomplete("fileTypes", [".csv", ".xtf"]);

    // Resets all fields and validations.
    cy.dataCy("reset-button").click();
    hasError("name", false);
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);
    hasError("evaluatePrecursorDelivery", false);
    hasError("evaluatePartial", false);
    hasError("evaluateComment", false);
    evaluateInput("name", "");
    evaluateAutocomplete("organisations", []);
    evaluateAutocomplete("fileTypes", []);
    evaluateInput("extent-bottom-left-longitude", "");
    evaluateInput("extent-bottom-left-latitude", "");
    evaluateInput("extent-upper-right-longitude", "");
    evaluateInput("extent-upper-right-latitude", "");
    evaluateSelect("evaluatePrecursorDelivery", "");
    evaluateSelect("evaluatePartial", "");
    evaluateSelect("evaluateComment", "");

    setInput("name", randomMandateName);
    setAutocomplete("organisations", "Brown and Sons");
    setAutocomplete("fileTypes", ".csv");
    setAutocomplete("fileTypes", ".xtf");
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("evaluatePrecursorDelivery", 0, 3);
    setSelect("evaluatePartial", 1, 2);
    setSelect("evaluateComment", 1, 3);

    cy.dataCy("save-button").click();
    cy.wait("@saveNew");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });

    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    setAutocomplete("fileTypes", ".xml");
    cy.wait(500);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("backToMandates-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    cy.dataCy("mandates-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").click();
    cy.contains(randomMandateName);
  });

  it("can edit existing mandate", () => {
    const randomMandateName = getRandomManadateName();
    cy.intercept({ url: "/api/v1/mandate", method: "POST" }).as("saveNew");
    cy.intercept({ url: "/api/v1/mandate", method: "PUT" }).as("updateMandate");

    // Create new mandate for testing
    cy.dataCy("addMandate-button").click();
    setInput("name", randomMandateName);
    setAutocomplete("organisations", "Schumm, Runte and Macejkovic");
    setAutocomplete("fileTypes", ".csv");
    setAutocomplete("fileTypes", ".xtf");
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("evaluatePrecursorDelivery", 1, 3);
    setSelect("evaluatePartial", 1, 2);
    setSelect("evaluateComment", 0, 3);
    cy.dataCy("backToMandates-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.wait("@saveNew");

    // Test editing the mandate
    cy.dataCy("mandates-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").click();
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").contains(randomMandateName).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });

    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");
    setAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Schumm, Runte and Macejkovic", "Brown and Sons"]);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    setInput("extent-bottom-left-latitude", "");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.disabled");

    setInput("extent-bottom-left-latitude", "47.23");
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    setSelect("evaluatePartial", 0, 2);

    cy.dataCy("save-button").click();
    cy.wait("@updateMandate");
    cy.wait(500); // Wait for the form to reset.
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    cy.dataCy("backToMandates-button").click();
    isPromptVisible(false);
    cy.dataCy("mandates-grid").find(".MuiTablePagination-actions [aria-label='Go to next page']").click();
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").last().contains("Schumm, Runte and Macejkovic");
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").last().contains("Brown and Sons");
  });
});
