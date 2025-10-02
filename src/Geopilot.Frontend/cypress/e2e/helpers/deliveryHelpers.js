import { toggleCheckbox, setSelect } from "./formHelpers.js";
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
  cy.intercept("POST", "/api/v1/validation").as("upload");
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

export const selectMandate = (index, expected) => {
  setSelect("mandate", index, expected);
};

export const startValidation = () => {
  cy.intercept("PATCH", "/api/v1/validation/*").as("startValidation");
  cy.dataCy("validate-button").click();
  cy.wait("@startValidation");
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
