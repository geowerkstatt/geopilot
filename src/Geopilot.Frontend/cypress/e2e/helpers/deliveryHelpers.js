import { loginAsUploader } from "./appHelpers.js";
import { toggleCheckbox } from "./formHelpers.js";

export const fileNameExists = (filePath, success) => {
  const fileName = filePath.split("/").pop();
  if (success) {
    cy.contains(fileName);
  } else {
    cy.contains(fileName).should("not.exist");
  }
};

export const addFile = (filePath, success) => {
  const mapPath = path => `cypress/fixtures/${path}`;
  const files = Array.isArray(filePath) ? filePath.map(mapPath) : mapPath(filePath);
  cy.dataCy("file-dropzone").selectFile(files, { action: "drag-drop" });
  Array.isArray(filePath) ? filePath.forEach(file => fileNameExists(file, success)) : fileNameExists(filePath, success);
};

export const uploadFile = () => {
  cy.intercept("POST", "/api/v2/upload").as("upload");
  cy.dataCy("acceptTermsOfUse-formCheckbox").then($checkbox => {
    if (!$checkbox.hasClass("Mui-checked")) {
      cy.dataCy("upload-button").should("be.disabled");
      toggleCheckbox("acceptTermsOfUse");
      cy.dataCy("upload-button").should("be.enabled");
    }
    cy.dataCy("upload-button").click();
  });
  cy.wait("@upload");
};

export const selectMandate = id => {
  cy.wait(200);
  cy.dataCy("mandate-selection-group").dataCy(`mandate-${id}`).click();
};

export const startProcessing = () => {
  cy.intercept("POST", "/api/v2/processing").as("startProcessing");
  cy.dataCy("startProcessing-button").click();
  cy.wait("@startProcessing");
};

export const stepIsActive = (stepName, isActive = true) => {
  if (isActive) {
    cy.dataCy(`${stepName}-step`).should("have.attr", "aria-current", "step");
  } else {
    cy.dataCy(`${stepName}-step`).should("not.have.attr", "aria-current");
  }
};

export const stepIsLoading = (stepName, isLoading = true) => {
  if (isLoading) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-loading").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-loading").should("not.exist");
  }
};

export const stepHasError = (stepName, hasError, errorText) => {
  if (hasError) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-error").should("exist");
    cy.dataCy(`${stepName}-step`).contains(errorText);
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-error").should("not.exist");
  }
};

export const stepIsSkipped = (stepName, isSkipped = true, text) => {
  if (isSkipped) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-skipped").should("exist");
    if (text) {
      cy.dataCy(`${stepName}-step`).contains(text);
    }
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-skipped").should("not.exist");
  }
};

export const stepIsCompleted = (stepName, isCompleted = true) => {
  if (isCompleted) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-success").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-success").should("not.exist");
  }
};

export const selectStep = stepName => {
  cy.dataCy(`${stepName}-step`).click();
};

/**
 * Builds a single pipeline-step result for a mocked processing job. Omitting `message` leaves the step
 * without a condition message.
 */
export const processingStep = (id, name, state, message, deliveries = []) => ({
  id,
  name: { en: name, de: name },
  state,
  ...(message ? { conditionMessage: { en: message, de: message } } : {}),
  downloads: [],
  deliveries,
  visualizations: [],
});

/**
 * Builds a mocked processing-job response for mandate 1 with the given aggregate state and steps.
 */
export const processingJob = (jobId, state, steps) => ({
  jobId,
  state,
  mandateId: 1,
  pipelineName: { en: "XTF Validation", de: "XTF Validierung" },
  steps,
});

/**
 * Builds a mandate that allows delivery and requires no delivery-form input (every field is "notEvaluated"),
 * so the create-delivery button is enabled without filling anything. Use it to stub the mandate list when a
 * test needs a deterministic delivery form rather than the randomly seeded mandate config.
 */
export const deliverableMandate = (id, name) => ({
  id,
  name: typeof name === "string" ? { en: name, de: name } : name,
  description: {},
  allowDelivery: true,
  evaluatePrecursorDelivery: "notEvaluated",
  evaluatePartial: "notEvaluated",
  evaluateComment: "notEvaluated",
});

/**
 * Logs in, uploads a valid file, selects mandate 1 and starts processing, returning the given mocked job as
 * the response for both the POST and the status GET. Leaves the wizard on the processing step with the job
 * status loaded. Pass `mandates` to stub the mandate list (otherwise the real seeded mandates are used).
 */
export const runMockedProcessingJob = (job, mandates) => {
  loginAsUploader();
  addFile("deliveryFiles/ilimodels_valid.xtf", true);
  uploadFile();

  if (mandates) {
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*", { statusCode: 200, body: mandates }).as("getMandates");
  } else {
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");
  }
  cy.wait("@getMandates");
  if (mandates) {
    for (const mandate of mandates) {
      cy.dataCy("mandate-selection-group").contains(mandate.name.en).should("exist");
    }
  }
  selectMandate(1);

  cy.intercept("POST", "/api/v2/processing", { statusCode: 200, body: job }).as("startProcessing");
  cy.intercept("GET", "/api/v2/processing/*", { statusCode: 200, body: job }).as("jobStatus");

  cy.dataCy("startProcessing-button").click();
  cy.wait("@startProcessing");
  cy.wait("@jobStatus");
};

/** Asserts the results-pane accordion for a pipeline step shows the icon for the given state. */
export const resultStepHasIcon = (stepId, state) => {
  cy.dataCy(`processing-step-${stepId}`).dataCy(`stepIcon-${state}`).should("exist");
};

/** Asserts the results-pane accordion for a pipeline step contains the given text. */
export const resultStepShowsMessage = (stepId, text) => {
  cy.dataCy(`processing-step-${stepId}`).contains(text);
};

/** Asserts the stepper node for a wizard step shows the icon for the given state. */
export const stepperStepHasIcon = (stepName, state) => {
  cy.dataCy(`${stepName}-step`).dataCy(`stepIcon-${state}`).should("exist");
};

/** Asserts the stepper node for a wizard step does not show the icon for the given state. */
export const stepperStepMissingIcon = (stepName, state) => {
  cy.dataCy(`${stepName}-step`).dataCy(`stepIcon-${state}`).should("not.exist");
};

/** Asserts the stepper node for a wizard step contains the given text. */
export const stepperStepShowsMessage = (stepName, text) => {
  cy.dataCy(`${stepName}-step`).contains(text);
};

/** Asserts the stepper node for a wizard step does not contain the given text. */
export const stepperStepMissingMessage = (stepName, text) => {
  cy.dataCy(`${stepName}-step`).should("not.contain", text);
};
