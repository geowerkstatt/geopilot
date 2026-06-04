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
  cy.dataCy("file-dropzone").attachFile(filePath, { subjectType: "drag-n-drop" });
  Array.isArray(filePath) ? filePath.forEach(file => fileNameExists(file, success)) : fileNameExists(filePath, success);
};

export const uploadFile = () => {
  cy.intercept("POST", "/api/v2/processing").as("upload");
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

export const selectMandate = (id, expected) => {
  cy.dataCy("mandate-selection-group").find(`[data-cy^="mandate-"]`).should("have.length", expected);
  cy.wait(200);
  cy.dataCy("mandate-selection-group").dataCy(`mandate-${id}`).click();
};

export const startProcessing = () => {
  cy.intercept("PATCH", "/api/v2/processing/*").as("startProcessing");
  cy.dataCy("startProcessing-button").click();
  cy.wait("@startProcessing");
};

export const stepIsActive = (stepName, isActive = true) => {
  if (isActive) {
    cy.dataCy(`${stepName}-step`).dataCy("active").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("active").should("not.exist");
  }
};

export const stepIsLoading = (stepName, isLoading = true) => {
  if (isLoading) {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-loading").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-loading").should("not.exist");
  }
};

export const stepHasError = (stepName, hasError, errorText) => {
  if (hasError) {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-error").should("exist");
    cy.dataCy(`${stepName}-step`).contains(errorText);
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-error").should("not.exist");
  }
};

export const stepIsCompleted = (stepName, isCompleted = true) => {
  if (isCompleted) {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-completed").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).dataCy("stepper-completed").should("not.exist");
  }
};

export const selectStep = stepName => {
  cy.dataCy(`${stepName}-step`).click();
};
