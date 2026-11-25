import { getGridRowThatContains, isSelectedNavItem, loginAsAdmin } from "./helpers/appHelpers";
import {
  evaluateChipInput,
  evaluateInput,
  getFormField,
  hasError,
  removeChipInputValue,
  setChipInput,
  setInput,
} from "./helpers/formHelpers";
import { handlePrompt } from "./helpers/promptHelpers";

// Scope is unique, so every test brings its own.
const getRandomScope = () => `Scope-${Math.random().toString(36).substring(2, 15)}`;

const createRefMapping = (scope: string, refDataUris: string[]) => {
  cy.intercept({ url: "/api/v1/refmapping", method: "POST" }).as("saveNew");
  cy.dataCy("addRefMapping-button").click();
  setInput("scope", scope);
  refDataUris.forEach(uri => setChipInput("refDataUris", uri));
  cy.dataCy("save-button").click();
  cy.wait("@saveNew").its("response.statusCode").should("eq", 201);
  cy.location().should(location => {
    expect(location.pathname).to.eq(`/admin/refmappings`);
  });
};

// The dev stack switches the reference data mapping on, which is what routes the page and shows it in the navigation.
describe("Reference data mapping tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/refmappings");
    isSelectedNavItem("admin-refmappings-nav", "admin-navigation");
  });

  it("displays the seeded entry in a list", () => {
    cy.dataCy("refMappings-grid").should("exist");
    getGridRowThatContains("refMappings-grid", "449").contains("ilidata:dmav_LFP3_449");
  });

  it("can create an entry", () => {
    const scope = getRandomScope();

    cy.dataCy("addRefMapping-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/refmappings/0`);
    });
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");
    cy.dataCy("delete-button").should("not.exist");

    // Scope and at least one reference are required.
    setInput("comment", "Created by Cypress");
    cy.dataCy("save-button").click();
    hasError("scope", true);
    hasError("refDataUris", true);
    cy.dataCy("save-button").should("be.disabled");

    setInput("scope", scope);
    hasError("scope", false);
    setChipInput("refDataUris", "ilidata:dmav_LFP1_V1_0");
    setChipInput("refDataUris", "ilidata:dmav_HFP1_V1_0");
    hasError("refDataUris", false);
    evaluateChipInput("refDataUris", ["ilidata:dmav_LFP1_V1_0", "ilidata:dmav_HFP1_V1_0"]);

    cy.intercept({ url: "/api/v1/refmapping", method: "POST" }).as("saveNew");
    cy.dataCy("save-button").click();
    cy.wait("@saveNew").then(({ response }) => {
      expect(response).to.not.be.undefined;
      expect(response!.statusCode).to.eq(201);
      expect(response!.body.comment).to.eq("Created by Cypress");
    });
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/refmappings`);
    });
    getGridRowThatContains("refMappings-grid", scope).contains("ilidata:dmav_LFP1_V1_0, ilidata:dmav_HFP1_V1_0");
  });

  it("can edit an entry", () => {
    const scope = getRandomScope();
    createRefMapping(scope, ["ilidata:dmav_LFP1_V1_0", "ilidata:dmav_HFP1_V1_0"]);

    getGridRowThatContains("refMappings-grid", scope).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/refmappings\/[1-9]\d*/);
    });
    evaluateInput("scope", scope);
    evaluateChipInput("refDataUris", ["ilidata:dmav_LFP1_V1_0", "ilidata:dmav_HFP1_V1_0"]);
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    cy.intercept({ url: "/api/v1/refmapping", method: "PUT" }).as("update");
    setInput("scope", `${scope}-updated`);
    removeChipInputValue("refDataUris", "ilidata:dmav_HFP1_V1_0");
    setChipInput("refDataUris", "ilidata:dmav_LFP2_BE");
    setInput("comment", "Updated by Cypress");
    cy.dataCy("save-button").click();
    cy.wait("@update").then(({ response }) => {
      expect(response).to.not.be.undefined;
      expect(response!.statusCode).to.eq(200);
      expect(response!.body.comment).to.eq("Updated by Cypress");
    });

    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/refmappings`);
    });
    getGridRowThatContains("refMappings-grid", `${scope}-updated`).contains(
      "ilidata:dmav_LFP1_V1_0, ilidata:dmav_LFP2_BE",
    );

    getGridRowThatContains("refMappings-grid", `${scope}-updated`).click();
    getFormField("comment").find("textarea").first().should("have.value", "Updated by Cypress");
  });

  it("can delete an entry", () => {
    const scope = getRandomScope();
    createRefMapping(scope, ["ilidata:dmav_LFP1_V1_0"]);

    getGridRowThatContains("refMappings-grid", scope).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/refmappings\/[1-9]\d*/);
    });

    cy.intercept({ url: "/api/v1/refmapping/*", method: "DELETE" }).as("delete");
    const confirmation = "Do you really want to delete the reference data? This action cannot be undone.";
    cy.dataCy("delete-button").click();
    handlePrompt(confirmation, "cancel");
    evaluateInput("scope", scope);

    cy.dataCy("delete-button").click();
    handlePrompt(confirmation, "delete");
    cy.wait("@delete").its("response.statusCode").should("eq", 204);
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/refmappings`);
    });
    cy.dataCy("refMappings-grid").find(".MuiDataGrid-row:not(.MuiDataGrid-rowSkeleton)").should("exist");
    cy.dataCy("refMappings-grid").contains(scope).should("not.exist");
  });

  it("refuses a reference that is not of the form ilidata:<Id>", () => {
    const scope = getRandomScope();
    cy.intercept({ url: "/api/v1/refmapping", method: "POST" }).as("saveNew");

    cy.dataCy("addRefMapping-button").click();
    setInput("scope", scope);
    setChipInput("refDataUris", "https://example.com/refdata.xtf");
    hasError("refDataUris");
    cy.dataCy("save-button").should("be.disabled");
    setChipInput("refDataUris", "ilidata:invalid$character"); // The id is only checked in the backend
    hasError("refDataUris", false);
    cy.dataCy("save-button").click();
    cy.wait("@saveNew").its("response.statusCode").should("eq", 400);

    // The form stays open with the refused entry, so the admin can correct it.
    cy.get(".MuiAlert-message").should(
      "contain.text",
      "The reference <ilidata:invalid$character> is not of the form ilidata:<Id>.",
    );
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/refmappings/0`);
    });

    removeChipInputValue("refDataUris", "ilidata:invalid$character");
    setChipInput("refDataUris", "ilidata:dmav_LFP1_V1_0");
    cy.dataCy("save-button").click();
    cy.wait("@saveNew").its("response.statusCode").should("eq", 201);
    getGridRowThatContains("refMappings-grid", scope).should("exist");
  });
});
