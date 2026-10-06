import type {
  Mandate,
  ProcessingJobResponse,
  ProcessingState,
  StepResultResponse,
  StepState,
} from "geopilot/api/generated";
import { loginAsUploader } from "./appHelpers";
import { toggleCheckbox } from "./formHelpers";

export const fileNameExists = (filePath: string, success: boolean) => {
  const fileName = filePath.split("/").pop()!;
  if (success) {
    cy.contains(fileName);
  } else {
    cy.contains(fileName).should("not.exist");
  }
};

export const addFile = (filePath: string | string[], success: boolean) => {
  const mapPath = (path: string) => `cypress/fixtures/${path}`;
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

export const selectMandate = (id: number) => {
  cy.wait(200);
  cy.dataCy("mandate-selection-group").dataCy(`mandate-${id}`).click();
};

export const startProcessing = () => {
  cy.intercept("POST", "/api/v2/processing").as("startProcessing");
  cy.intercept("GET", "/api/v2/processing/*").as("jobStatus");
  cy.dataCy("startProcessing-button").click();
  cy.wait("@startProcessing");
};

/**
 * Waits until the status polling started by `startProcessing` reports the job as finished. A real pipeline
 * run takes longer than the default command timeout, so every poll gets its own generous limit.
 */
export const waitForProcessingToFinish = () => {
  cy.wait<unknown, ProcessingJobResponse>("@jobStatus", { timeout: 120000 }).then(({ response }) => {
    if (["pending", "running"].includes(response!.body.state)) {
      waitForProcessingToFinish();
    }
  });
};

export const stepIsActive = (stepName: string, isActive = true) => {
  if (isActive) {
    cy.dataCy(`${stepName}-step`).should("have.attr", "aria-current", "step");
  } else {
    cy.dataCy(`${stepName}-step`).should("not.have.attr", "aria-current");
  }
};

export const stepIsLoading = (stepName: string, isLoading = true) => {
  if (isLoading) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-loading").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-loading").should("not.exist");
  }
};

type StepHasErrorFunction = {
  (stepName: string, hasError: false): void;
  (stepName: string, hasError: true, errorText: string | RegExp): void;
};

export const stepHasError: StepHasErrorFunction = (
  stepName: string,
  hasError: boolean,
  errorText?: string | RegExp,
) => {
  if (hasError) {
    if (!errorText) throw new Error("Error text must be provided to stepHasError when hasError is true");
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-error").should("exist");
    cy.dataCy(`${stepName}-step`).contains(errorText);
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-error").should("not.exist");
  }
};

export const stepIsSkipped = (stepName: string, isSkipped = true, text?: string) => {
  if (isSkipped) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-skipped").should("exist");
    if (text) {
      cy.dataCy(`${stepName}-step`).contains(text);
    }
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-skipped").should("not.exist");
  }
};

export const stepIsCompleted = (stepName: string, isCompleted = true) => {
  if (isCompleted) {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-success").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepIcon-success").should("not.exist");
  }
};

export const selectStep = (stepName: string) => {
  cy.dataCy(`${stepName}-step`).click();
};

/**
 * Builds a single pipeline-step result for a mocked processing job. Omitting `message` leaves the step
 * without a condition message.
 */
export const processingStep = (
  id: string,
  name: string,
  state: StepState,
  message?: string,
  deliveries: string[] = [],
): StepResultResponse => ({
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
export const processingJob = (
  jobId: string,
  state: ProcessingState,
  steps: StepResultResponse[],
): ProcessingJobResponse => ({
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
export const deliverableMandate = (id: number, name: string | { en: string; de: string }): Mandate => ({
  id,
  name: typeof name === "string" ? { en: name, de: name } : name,
  description: {},
  isPublic: false,
  allowDelivery: true,
  fileTypes: [".*"],
  coordinates: [],
  evaluatePrecursorDelivery: "notEvaluated",
  evaluatePartial: "notEvaluated",
  evaluateComment: "notEvaluated",
  organisations: [],
  deliveries: [],
});

/**
 * Builds a mandate that allows no delivery, so the wizard omits the delivery step and the processing step
 * becomes the last one.
 */
export const nonDeliverableMandate = (id: number, name: string | { en: string; de: string }): Mandate => ({
  ...deliverableMandate(id, name),
  allowDelivery: false,
});

/**
 * Logs in, uploads a valid file, selects mandate 1 and starts processing, returning the given mocked job as
 * the response for both the POST and the status GET. Leaves the wizard on the processing step with the job
 * status loaded. Pass `mandates` to stub the mandate list (otherwise the real seeded mandates are used).
 *
 * Pass `runningJob` to answer the start request and the first status poll with an unfinished job, so a test
 * can assert on the running state before waiting for the next poll, which then delivers `job`.
 */
export const runMockedProcessingJob = (
  job: ProcessingJobResponse,
  mandates?: Mandate[],
  runningJob?: ProcessingJobResponse,
) => {
  // Registered before the upload: the mandate request follows the upload response immediately,
  // so an intercept set up afterwards can miss it.
  if (mandates) {
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*", { statusCode: 200, body: mandates }).as("getMandates");
  } else {
    cy.intercept("GET", "/api/v1/mandate/summary?uploadId=*").as("getMandates");
  }

  loginAsUploader();
  addFile("deliveryFiles/ilimodels_valid.xtf", true);
  uploadFile();
  cy.wait("@getMandates");
  if (mandates) {
    for (const mandate of mandates) {
      cy.dataCy("mandate-selection-group").contains(mandate.name.en).should("exist");
    }
  }
  selectMandate(1);

  const firstStatus = runningJob ?? job;
  let firstStatusServed = false;

  cy.intercept("POST", "/api/v2/processing", { statusCode: 200, body: firstStatus }).as("startProcessing");
  cy.intercept("GET", "/api/v2/processing/*", request => {
    request.reply({ statusCode: 200, body: firstStatusServed ? job : firstStatus });
    firstStatusServed = true;
  }).as("jobStatus");

  cy.dataCy("startProcessing-button").click();
  cy.wait("@startProcessing");
  cy.wait("@jobStatus");
};

/** Asserts the results-pane accordion for a pipeline step shows the icon for the given state. */
export const resultStepHasIcon = (stepId: string, state: string) => {
  cy.dataCy(`processing-step-${stepId}`).dataCy(`stepIcon-${state}`).should("exist");
};

/** Asserts the results-pane accordion for a pipeline step contains the given text. */
export const resultStepShowsMessage = (stepId: string, text: string) => {
  cy.dataCy(`processing-step-${stepId}`).contains(text);
};

/** Asserts the stepper node for a wizard step shows the icon for the given state. */
export const stepperStepHasIcon = (stepName: string, state: string) => {
  cy.dataCy(`${stepName}-step`).dataCy(`stepIcon-${state}`).should("exist");
};

/** Asserts the stepper node for a wizard step does not show the icon for the given state. */
export const stepperStepMissingIcon = (stepName: string, state: string) => {
  cy.dataCy(`${stepName}-step`).dataCy(`stepIcon-${state}`).should("not.exist");
};

/** Asserts the stepper node for a wizard step contains the given text. */
export const stepperStepShowsMessage = (stepName: string, text: string) => {
  cy.dataCy(`${stepName}-step`).contains(text);
};

/** Asserts the stepper node for a wizard step does not contain the given text. */
export const stepperStepMissingMessage = (stepName: string, text: string) => {
  cy.dataCy(`${stepName}-step`).should("not.contain", text);
};
