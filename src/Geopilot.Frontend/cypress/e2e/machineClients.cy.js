import { getGridRowThatContains, isSelectedNavItem, loginAsAdmin } from "./helpers/appHelpers.js";
import {
  evaluateAutocomplete,
  evaluateCheckbox,
  evaluateInput,
  getFormField,
  hasError,
  setAutocomplete,
  setInput,
  toggleCheckbox,
} from "./helpers/formHelpers.js";
import { handlePrompt } from "./helpers/promptHelpers.js";

const getRandomName = () => `Client-${Math.random().toString(36).substring(2, 15)}`;
const getRandomIdentifier = () => `sub-${Math.random().toString(36).substring(2, 15)}`;

// The dev stack offers machine delivery, which is what routes the administration and shows it in the navigation.
describe("Machine clients tests", () => {
  beforeEach(() => {
    loginAsAdmin();
    cy.visit("/admin/machine-clients");
    isSelectedNavItem("admin-machine-clients-nav", "admin-navigation");
  });

  it("displays the machine clients in a list", () => {
    cy.dataCy("machineClients-grid").should("exist");
    cy.dataCy("addMachineClient-button").should("exist");
  });

  it("can register a machine client for an organisation", () => {
    const name = getRandomName();
    const identifier = getRandomIdentifier();
    cy.intercept({ url: "/api/v1/machineclient", method: "POST" }).as("saveNew");

    cy.dataCy("addMachineClient-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/machine-clients/0`);
    });
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    // A client needs both a name and the identifier its token carries.
    setInput("name", name);
    cy.dataCy("save-button").click();
    hasError("authIdentifier", true);
    cy.dataCy("save-button").should("be.disabled");

    setInput("authIdentifier", identifier);
    hasError("authIdentifier", false);
    evaluateCheckbox("isActive", true);
    setAutocomplete("organisations", "Brown and Sons");
    cy.dataCy("save-button").should("be.enabled");
    cy.dataCy("save-button").click();
    cy.wait("@saveNew").its("response.statusCode").should("eq", 201);

    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/machine-clients`);
    });
    getGridRowThatContains("machineClients-grid", name).contains(identifier);
    getGridRowThatContains("machineClients-grid", name).contains("Brown and Sons");

    // The assignment is visible from the organisation as well.
    cy.dataCy("admin-organisations-nav").click();
    getGridRowThatContains("organisations-grid", "Brown and Sons").click();
    getFormField("machineClients").contains(name);
  });

  it("can edit a machine client and deactivate it", () => {
    const name = getRandomName();
    cy.intercept({ url: "/api/v1/machineclient", method: "POST" }).as("saveNew");
    cy.intercept({ url: "/api/v1/machineclient", method: "PUT" }).as("update");

    cy.dataCy("addMachineClient-button").click();
    setInput("name", name);
    setInput("authIdentifier", getRandomIdentifier());
    cy.dataCy("backToMachineClients-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "save");
    cy.wait("@saveNew");

    getGridRowThatContains("machineClients-grid", name).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/machine-clients\/[1-9]\d*/);
    });
    evaluateInput("name", name);
    evaluateAutocomplete("organisations", []);

    setInput("name", `${name} updated`);
    toggleCheckbox("isActive");
    cy.dataCy("save-button").click();
    cy.wait("@update").then(({ response }) => {
      expect(response.statusCode).to.eq(200);
      expect(response.body.state).to.eq("inactive");
    });

    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/machine-clients`);
    });
    getGridRowThatContains("machineClients-grid", `${name} updated`).contains("Inactive");
  });
});
