import { loadWithoutAuth, loginAsUploader } from "./helpers/appHelpers.js";
import { clickCancel } from "./helpers/buttonHelpers.js";
import { hasError, setInput, setSelect, toggleCheckbox } from "./helpers/formHelpers.js";

const fileNameExists = (filePath, success) => {
  const fileName = filePath.split("/").pop();
  if (success) {
    cy.contains(fileName);
  } else {
    cy.contains(fileName).should("not.exist");
  }
};

const addFile = (filePath, success) => {
  cy.get('[data-cy="file-dropzone"]').attachFile(filePath, { subjectType: "drag-n-drop" });
  Array.isArray(filePath) ? filePath.forEach(file => fileNameExists(file, success)) : fileNameExists(filePath, success);
};

const uploadFile = () => {
  cy.get('[data-cy="acceptTermsOfUse-formCheckbox"]').then($checkbox => {
    if (!$checkbox.hasClass("Mui-checked")) {
      toggleCheckbox("acceptTermsOfUse");
    }
    cy.get('[data-cy="upload-button"]').click();
    stepIsLoading("upload");
  });
};

const resetDelivery = activeStep => {
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

const stepIsActive = (stepName, isActive = true) => {
  if (isActive) {
    cy.get(`[data-cy="${stepName}-step"] .MuiStepLabel-iconContainer.Mui-active`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] .MuiStepLabel-iconContainer.Mui-active`).should("not.exist");
  }
};

const stepIsLoading = (stepName, isLoading = true) => {
  if (isLoading) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-loading"]`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-loading"]`).should("not.exist");
  }
};

const stepHasError = (stepName, hasError, errorText) => {
  if (hasError) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-error"]`).should("exist");
    cy.get(`[data-cy="${stepName}-step"]`).contains(errorText);
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-error"]`).should("not.exist");
  }
};

const stepIsCompleted = (stepName, isCompleted = true) => {
  if (isCompleted) {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-completed"]`).should("exist");
  } else {
    cy.get(`[data-cy="${stepName}-step"] [data-cy="stepper-completed"]`).should("not.exist");
  }
};

describe("Delivery tests", () => {
  it("shows only validation steps if auth settings could not be loaded", () => {
    loadWithoutAuth();
    cy.get('[data-cy="upload-step"]').should("exist");
    cy.get('[data-cy="validate-step"]').should("exist");
    cy.get('[data-cy="submit-step"]').should("not.exist");
    cy.get('[data-cy="done-step"]').should("not.exist");
    stepIsActive("upload");

    // Limit the file types to a few extensions
    cy.intercept("/api/v1/validation", {
      statusCode: 200,
      body: { allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"] },
    }).as("fileExtensions");
    cy.wait("@fileExtensions");
    cy.contains(".csv, .gpkg, .itf, .xml, .xtf or .zip (max. 200 MB)");

    addFile("deliveryFiles/invalid-type.png", false);
    stepHasError("upload", true, "The file type is not supported");

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_valid.xml"], false);
    stepHasError("upload", true, "Only one file can be checked at a time");

    // Cypress doesn't support attaching large files, so we cannot test the file size validation here.
  });

  it("can submit a delivery", () => {
    loginAsUploader();
    // All steps are visible
    cy.get('[data-cy="upload-step"]').should("exist");
    cy.get('[data-cy="validate-step"]').should("exist");
    cy.get('[data-cy="submit-step"]').should("exist");
    cy.get('[data-cy="done-step"]').should("exist");
    stepIsActive("upload");

    // All file types are allowed
    addFile("deliveryFiles/invalid-type.png", true);
    stepHasError("upload", false);

    // Selected file can be removed
    cy.get('[data-cy="file-remove-button"]').click();
    cy.contains("invalid-type.png").should("not.exist");

    // Can upload a file once the terms of use are accepted and the file is valid
    cy.get('[data-cy="upload-button"]').should("be.disabled");
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.get('[data-cy="upload-button"]').should("be.disabled");
    toggleCheckbox("acceptTermsOfUse");
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();

    // Can cancel the upload which will reset the form
    resetDelivery("upload");
    cy.contains("ilimodels_not_conform.xml").should("not.exist");

    // Validation starts automatically after a file is uploaded
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();
    cy.wait("@validation_post");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml").should("not.exist");

    // Shows validation errors
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait(500);
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.get('[data-cy="validate-step"]').contains("Die XML-Struktur der Transferdatei ist ungültig.");
    cy.get('[data-cy="Log-button"').should("not.exist");
    cy.get('[data-cy="Xtf-Log-button"').should("not.exist");
    stepIsActive("submit", false); // Should not be active if validation has errors

    resetDelivery("validate");
    addFile("deliveryFiles/ilimodels_invalid.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait(500);
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.get('[data-cy="validate-step"]').contains("Die Daten sind nicht modellkonform.");
    cy.get('[data-cy="Log-button"').should("exist");
    cy.get('[data-cy="Xtf-Log-button"').should("exist");
    stepIsActive("submit", false); // Should not be active if validation has errors

    // Submit is active if validation is successful
    resetDelivery("validate");
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait(500);
    stepIsLoading("validate", false);
    stepHasError("validate", false);
    cy.get('[data-cy="validate-step"]').contains("Die Daten sind modellkonform.");
    cy.get('[data-cy="Log-button"').should("exist");
    cy.get('[data-cy="Xtf-Log-button"').should("exist");
    cy.get('[data-cy="validate-step"] [data-cy="cancel-button"]').should("not.exist");
    stepIsCompleted("validate");
    stepIsActive("submit");

    // Submit can be cancelled, which will return to step 1 with a reset form
    resetDelivery("submit");

    // Submitting the delivery
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    cy.wait(500);
    stepIsCompleted("validate");
    stepIsActive("submit");

    cy.get('[data-cy="createDelivery-button"]').should("be.disabled");
    setSelect("mandate", 1, 4);
    cy.get('[data-cy="createDelivery-button"]').should("be.enabled");
    setSelect("mandate", 0);
    cy.get('[data-cy="createDelivery-button"]').should("be.disabled");
    hasError("mandate");
    setSelect("mandate", 2);
    setSelect("predecessor", 2);
    setSelect("predecessor", 0);
    hasError("predecessor", false);
    cy.get('[data-cy="createDelivery-button"]').should("be.enabled");
    cy.get('[data-cy="createDelivery-button"]').click();
    stepIsLoading("submit");

    cy.wait("@delivery_post");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.get('[data-cy="done-step"]').contains("The delivery was completed successfully.");

    // Can restart the delivery process after submitting was successful
    cy.get('[data-cy="addAnotherDelivery-button"]').should("exist");
    cy.get('[data-cy="addAnotherDelivery-button"]').click();
    stepIsActive("upload");
    stepIsCompleted("upload", false);
    stepIsActive("validate", false);
    stepIsCompleted("validate", false);
    stepIsActive("submit", false);
    stepIsCompleted("submit", false);
    stepIsActive("done", false);
    stepIsCompleted("done", false);
    cy.get('[data-cy="upload-button"]').should("be.disabled");

    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    cy.wait(500);
    stepIsCompleted("validate");
    stepIsActive("submit");

    setSelect("mandate", 1);
    setSelect("predecessor", 1);
    toggleCheckbox("isPartial");
    setInput("comment", "This is a test comment.");
    cy.get('[data-cy="createDelivery-button"]').click();
    stepIsLoading("submit");
    cy.wait("@delivery_post");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.get('[data-cy="done-step"]').contains("The delivery was completed successfully.");
  });

  it("can log in during the delivery process", () => {
    cy.visit("/");
    cy.get('[data-cy="logIn-button"]').should("exist");

    cy.get('[data-cy="upload-step"]').should("exist");
    cy.get('[data-cy="validate-step"]').should("exist");
    cy.get('[data-cy="submit-step"]').should("exist");
    cy.get('[data-cy="done-step"]').should("exist");
    stepIsActive("upload");

    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@validation_post");
    cy.wait(500);
    stepIsCompleted("upload");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.get('[data-cy="logInForDelivery-button"]').should("exist");
  });
});
