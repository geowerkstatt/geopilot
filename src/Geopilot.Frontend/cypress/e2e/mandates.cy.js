import { getGridRowThatContains, isSelectedNavItem, loginAsAdmin, openTool } from "./helpers/appHelpers.js";
import {
  evaluateAutocomplete,
  evaluateInput,
  evaluateSelect,
  hasError,
  setFreeSoloAutocomplete,
  setInput,
  setNonFreeSoloAutocomplete,
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
    cy.dataCy("save-button").should("be.disabled");

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
    setSelect("pipelineId", 0, 1);
    setFreeSoloAutocomplete("fileTypes", ".xml");
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
    cy.dataCy("save-button").should("be.disabled");

    // Fields should not show errors before they are touched.
    hasError("name", false);
    hasError("pipelineId", false);
    hasError("fileTypes", false);
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
    hasError("pipelineId", true);
    hasError("fileTypes", true);
    hasError("evaluatePrecursorDelivery", true);
    hasError("evaluatePartial", true);
    hasError("evaluateComment", true);

    // Fill out all required fields while checking if errors disappear.
    setSelect("pipelineId", 0, 1);
    hasError("pipelineId", false);
    setFreeSoloAutocomplete("fileTypes", ".xml");
    setFreeSoloAutocomplete("fileTypes", ".xtf");
    evaluateAutocomplete("fileTypes", [".xml", ".xtf"]);
    setSelect("evaluatePrecursorDelivery", 0, 3);
    hasError("evaluatePrecursorDelivery", false);
    setSelect("evaluatePartial", 1, 2);
    hasError("evaluatePartial", false);
    setSelect("evaluateComment", 1, 3);
    hasError("evaluateComment", false);
    cy.dataCy("save-button").should("be.enabled");

    // Fill out optional fields.
    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Brown and Sons"]);

    // Resets all fields and validations.
    cy.dataCy("reset-button").click();
    hasError("name", false);
    hasError("pipelineId", false);
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

    // Fill out the entire form
    setInput("name", randomMandateName);
    setSelect("pipelineId", 0, 1);
    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    setFreeSoloAutocomplete("fileTypes", ".xml");
    setFreeSoloAutocomplete("fileTypes", ".xtf");
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("evaluatePrecursorDelivery", 0, 3);
    setSelect("evaluatePartial", 1, 2);
    setSelect("evaluateComment", 1, 3);

    // Save the mandate and check that we stay on the detail page of the new mandate.
    cy.dataCy("save-button").click();
    cy.wait("@saveNew");
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });

    // Buttons should be disabled again after save.
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    // Check that unsaved changes are not saved when navigating back to the list and choosing "reset" in the prompt.
    setFreeSoloAutocomplete("fileTypes", ".itf");
    cy.wait(500);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("backToMandates-button").click();
    handlePrompt("You have unsaved changes. How would you like to proceed?", "reset");
    getGridRowThatContains("mandates-grid", randomMandateName).contains(".itf").should("not.exist");
  });

  it("can edit existing mandate", () => {
    const randomMandateName = getRandomManadateName();
    cy.intercept({ url: "/api/v1/mandate", method: "POST" }).as("saveNew");
    cy.intercept({ url: "/api/v1/mandate", method: "PUT" }).as("updateMandate");
    cy.intercept({ url: "/api/v1/mandate", method: "GET" }).as("getMandates");

    // Create new mandate for testing
    cy.dataCy("addMandate-button").click();
    setInput("name", randomMandateName);
    setSelect("pipelineId", 0, 1);
    setNonFreeSoloAutocomplete("organisations", "Schumm, Runte and Macejkovic");
    setFreeSoloAutocomplete("fileTypes", ".xml");
    setFreeSoloAutocomplete("fileTypes", ".xtf");
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
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").contains(randomMandateName).click();
    cy.location().should(location => {
      expect(location.pathname).to.match(/\/admin\/mandates\/[1-9]\d*/);
    });

    // Buttons should be disabled at first when form is untouched.
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    // Make change and check if buttons are now enabled after change.
    setNonFreeSoloAutocomplete("organisations", "Brown and Sons");
    evaluateAutocomplete("organisations", ["Schumm, Runte and Macejkovic", "Brown and Sons"]);
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    // Make spatial extent invalid and check that errors are shown.
    setInput("extent-bottom-left-latitude", "");
    hasError("extent-bottom-left-longitude", true);
    hasError("extent-bottom-left-latitude", true);
    hasError("extent-upper-right-longitude", true);
    hasError("extent-upper-right-latitude", true);

    // Buttons should still be enabled because form is dirty, but save should be disabled due to validation errors.
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.disabled");

    // Make spatial extent valid again and check that errors are gone.
    setInput("extent-bottom-left-latitude", "47.23");
    hasError("extent-bottom-left-longitude", false);
    hasError("extent-bottom-left-latitude", false);
    hasError("extent-upper-right-longitude", false);
    hasError("extent-upper-right-latitude", false);

    // Buttons should be enabled again because form is dirty and valid.
    cy.dataCy("reset-button").should("be.enabled");
    cy.dataCy("save-button").should("be.enabled");

    // Change other fields as well.
    setSelect("evaluatePartial", 0, 2);

    // Save
    cy.dataCy("save-button").click();
    cy.wait("@updateMandate");
    cy.wait(500); // Wait for the form to reset.
    cy.dataCy("reset-button").should("be.disabled");
    cy.dataCy("save-button").should("be.disabled");

    // Go back to list and check that changes are saved.
    cy.dataCy("backToMandates-button").click();
    isPromptVisible(false);
    cy.wait("@getMandates");
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").last().contains("Schumm, Runte and Macejkovic");
    cy.dataCy("mandates-grid").find(".MuiDataGrid-row").last().contains("Brown and Sons");
  });

  it("prevents multiple save requests while waiting for the API response", () => {
    const randomMandateName = getRandomManadateName();

    // Intercept the POST call and simulate a delayed response.
    cy.intercept({ url: "/api/v1/mandate", method: "POST" }, req => {
      // Attach a delay to the response to simulate latency.
      req.on("response", res => {
        res.setDelay(2000); // Delay in milliseconds
      });
    }).as("slowSave");

    // Open new mandate form.
    cy.dataCy("addMandate-button").click();
    cy.location().should(location => {
      expect(location.pathname).to.eq(`/admin/mandates/0`);
    });

    // Fill in required fields.
    setInput("name", randomMandateName);
    setSelect("pipelineId", 0, 1);
    setFreeSoloAutocomplete("fileTypes", ".xml");
    setInput("extent-bottom-left-longitude", "7.3");
    setInput("extent-bottom-left-latitude", "47.13");
    setInput("extent-upper-right-longitude", "8.052");
    setInput("extent-upper-right-latitude", "47.46");
    setSelect("evaluatePrecursorDelivery", 0, 3);
    setSelect("evaluatePartial", 1, 2);
    setSelect("evaluateComment", 1, 3);

    // Click the save button twice in rapid succession.
    cy.dataCy("save-button").click({ force: true }).click({ force: true });

    // Wait for the slow save to complete.
    cy.wait("@slowSave").then(interception => {
      expect(interception).to.exist;
    });

    // Ensure that only one API call was made.
    cy.get("@slowSave.all").should("have.length", 1);
  });
});

describe("Mandate with a removed pipeline", () => {
  // A pipeline definition can be changed between restarts: a pipeline a mandate
  // was configured with may no longer exist. The stored pipelineId then dangles.
  const ghostPipelineId = "ghost-pipeline";
  const availablePipelines = {
    pipelines: [{ id: "valid-pipeline", displayName: { de: "Gültige Pipeline", en: "Valid Pipeline" } }],
  };
  const mandateWithGhostPipeline = {
    id: 9999,
    name: "Ghost Pipeline Mandate",
    isPublic: false,
    allowDelivery: true,
    fileTypes: [".xml"],
    coordinates: [
      { x: 7.3, y: 47.13 },
      { x: 8.05, y: 47.46 },
    ],
    organisations: [],
    deliveries: [],
    evaluatePrecursorDelivery: "optional",
    evaluatePartial: "required",
    evaluateComment: "notEvaluated",
    pipelineId: ghostPipelineId,
  };

  beforeEach(() => {
    loginAsAdmin();
    cy.intercept("GET", "/api/v1/pipeline", { statusCode: 200, body: availablePipelines }).as("getPipelines");
  });

  it("shows the missing pipeline as invalid in the edit form and blocks saving", () => {
    cy.intercept("GET", "/api/v1/mandate/9999", { statusCode: 200, body: mandateWithGhostPipeline }).as("getMandate");

    cy.visit("/admin/mandates/9999");
    cy.wait("@getMandate");
    cy.wait("@getPipelines");

    // The configured-but-missing pipeline must be surfaced, not rendered as a blank "nothing selected".
    cy.dataCy("pipelineId-formSelect").should("contain", ghostPipelineId);

    // The field must be in an error state so the mandate is flagged as needing correction.
    hasError("pipelineId", true);

    // Dirtying the form must not enable saving while the pipeline reference is invalid.
    setInput("name", "Ghost Pipeline Mandate (edited)");
    cy.dataCy("save-button").should("be.disabled");
  });

  it("flags a mandate in the overview whose pipeline no longer exists", () => {
    cy.intercept("GET", "/api/v1/mandate", {
      statusCode: 200,
      body: [
        { ...mandateWithGhostPipeline, id: 9001, name: "Healthy Mandate", pipelineId: "valid-pipeline" },
        { ...mandateWithGhostPipeline, id: 9002, name: "Ghost Pipeline Mandate" },
      ],
    }).as("getMandates");

    cy.visit("/admin/mandates");
    cy.wait("@getMandates");
    cy.wait("@getPipelines");

    // A healthy mandate resolves its pipeline to the localised display name.
    getGridRowThatContains("mandates-grid", "Healthy Mandate").should("contain", "Valid Pipeline");

    // The broken mandate is flagged with the id of the pipeline that no longer exists.
    getGridRowThatContains("mandates-grid", "Ghost Pipeline Mandate").should("contain", ghostPipelineId);
  });
});
