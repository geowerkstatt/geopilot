import { toggleCheckbox } from "./formHelpers.js";
import { clickCancel } from "./buttonHelpers.js";

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
  cy.dataCy("acceptTermsOfUse-formCheckbox").then($checkbox => {
    if (!$checkbox.hasClass("Mui-checked")) {
      toggleCheckbox("acceptTermsOfUse");
    }
    cy.dataCy("upload-button").click();
    stepIsLoading("upload");
  });
};

export const resetDelivery = activeStep => {
  cy.dataCy(`${activeStep}-step`).dataCy("cancel-button").should("exist");
  clickCancel(`${activeStep}-step`);
  stepIsActive("upload");
  stepIsCompleted("upload", false);
  stepIsActive("validate", false);
  stepIsCompleted("validate", false);
  stepIsActive("submit", false);
  stepIsCompleted("submit", false);
  stepIsActive("done", false);
  stepIsCompleted("done", false);
  cy.dataCy("upload-button").should("be.disabled");
};

export const stepIsActive = (stepName, isActive = true) => {
  if (isActive) {
    cy.dataCy(`${stepName}-step`).find(".MuiStepLabel-iconContainer.Mui-active").should("exist");
  } else {
    cy.dataCy(`${stepName}-step`).find(".MuiStepLabel-iconContainer.Mui-active").should("not.exist");
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
