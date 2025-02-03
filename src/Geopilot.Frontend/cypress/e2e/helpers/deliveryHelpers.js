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
  cy.get('[data-cy="file-dropzone"]').attachFile(filePath, { subjectType: "drag-n-drop" });
  Array.isArray(filePath) ? filePath.forEach(file => fileNameExists(file, success)) : fileNameExists(filePath, success);
};

export const uploadFile = () => {
  cy.get('[data-cy="acceptTermsOfUse-formCheckbox"]').then($checkbox => {
    if (!$checkbox.hasClass("Mui-checked")) {
      toggleCheckbox("acceptTermsOfUse");
    }
    cy.get('[data-cy="upload-button"]').click();
    stepIsLoading("upload");
  });
};

export const resetDelivery = activeStep => {
  cy.get(`[data-cy="${activeStep}-step"] [data-cy="cancel-button"]`).should("exist");
  clickCancel(`${activeStep}-step`);
  stepIsActive("upload");
  stepIsCompleted("upload", false);
  stepIsActive("validate", false);
  stepIsCompleted("validate", false);
  stepIsActive("submit", false);
  stepIsCompleted("submit", false);
  stepIsActive("done", false);
  stepIsCompleted("done", false);
  cy.get('[data-cy="upload-button"]').should("be.disabled");
};

export const stepIsActive = (stepName, isActive = true) => {
  if (isActive) {
    cy.get(`[data-cy="${stepName}-step"] .MuiStepLabel-iconContainer.Mui-active`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] .MuiStepLabel-iconContainer.Mui-active`).should("not.exist");
  }
};

export const stepIsLoading = (stepName, isLoading = true) => {
  if (isLoading) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-loading"]`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-loading"]`).should("not.exist");
  }
};

export const stepHasError = (stepName, hasError, errorText) => {
  if (hasError) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-error"]`).should("exist");
    cy.get(`[data-cy="${stepName}-step"]`).contains(errorText);
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-error"]`).should("not.exist");
  }
};

export const stepIsCompleted = (stepName, isCompleted = true) => {
  if (isCompleted) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-completed"]`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-completed"]`).should("not.exist");
  }
};
