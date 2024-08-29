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

const mockUploadSuccess = () => {
  cy.intercept({ url: "/api/v1/validation", method: "POST" }, req => {
    req.reply({
      statusCode: 201,
      body: {
        jobId: "d49ba857-5db5-45a0-b838-9d41cc7d8d64",
        status: "processing",
        validatorResults: {
          ilicheck: {
            status: "processing",
            statusMessage: "Die Datei wird validiert...",
            logFiles: {},
          },
        },
      },
      delay: 500,
    });
  }).as("upload");
};

const mockValidationSuccess = () => {
  cy.intercept({ url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" }, req => {
    req.reply({
      statusCode: 200,
      body: {
        jobId: "d49ba857-5db5-45a0-b838-9d41cc7d8d64",
        status: "completed",
        validatorResults: {
          ilicheck: {
            status: "completed",
            statusMessage: "Die Daten sind modellkonform.",
            logFiles: {
              Log: "log.log",
              "Xtf-Log": "log.xtf",
            },
          },
        },
      },
      delay: 500,
    });
  }).as("validation");
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

  it("shows validation error: invalid structure", () => {
    mockUploadSuccess();
    cy.intercept({ url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" }, req => {
      req.reply({
        statusCode: 200,
        body: {
          jobId: "b058ad11-7dc2-4456-9099-e525116d7e9b",
          status: "completedWithErrors",
          validatorResults: {
            ilicheck: {
              status: "completedWithErrors",
              statusMessage: "Die XML-Struktur der Transferdatei ist ungültig.",
              logFiles: {},
            },
          },
        },
        delay: 500,
      });
    }).as("validation");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_invalid.xml", true);
    uploadFile();
    stepIsLoading("upload");
    cy.wait("@upload");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.get('[data-cy="validate-step"]').contains("Die XML-Struktur der Transferdatei ist ungültig.");
    cy.get('[data-cy="Log-button"').should("not.exist");
    cy.get('[data-cy="Xtf-Log-button"').should("not.exist");
    stepIsActive("submit", false); // Should not be active if validation has errors
  });

  it("shows validation error: not conform", () => {
    mockUploadSuccess();
    cy.intercept({ url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" }, req => {
      req.reply({
        statusCode: 200,
        body: {
          jobId: "d49ba857-5db5-45a0-b838-9d41cc7d8d64",
          status: "completedWithErrors",
          validatorResults: {
            ilicheck: {
              status: "completedWithErrors",
              statusMessage: "Die Daten sind nicht modellkonform.",
              logFiles: {
                Log: "log.log",
                "Xtf-Log": "log.xtf",
              },
            },
          },
        },
        delay: 500,
      });
    }).as("validation");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    stepIsLoading("upload");
    cy.wait("@upload");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.get('[data-cy="validate-step"]').contains("Die Daten sind nicht modellkonform.");
    cy.get('[data-cy="Log-button"').should("exist");
    cy.get('[data-cy="Xtf-Log-button"').should("exist");
    stepIsActive("submit", false); // Should not be active if validation has errors
  });

  it("can submit a delivery", () => {
    cy.intercept({ url: "/api/v1/validation", method: "POST" }).as("upload");
    cy.intercept({ url: "/api/v1/validation", method: "GET" }).as("validation");
    cy.intercept({ url: "/api/v1/delivery", method: "POST" }).as("submit");

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
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_valid.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_valid.xml").should("not.exist");

    // Submit is active if validation is successful
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("validate", true);
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
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

    // Add delivery with minimal form
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
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

    cy.wait("@submit");
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

    // Add delivery with completed form
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
    stepIsCompleted("validate");
    stepIsActive("submit");

    setSelect("mandate", 1);
    setSelect("predecessor", 1);
    toggleCheckbox("isPartial");
    setInput("comment", "This is a test comment.");
    cy.get('[data-cy="createDelivery-button"]').click();
    stepIsLoading("submit");
    cy.wait("@submit");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.get('[data-cy="done-step"]').contains("The delivery was completed successfully.");
  });

  it("can log in during the delivery process", () => {
    mockUploadSuccess();
    mockValidationSuccess();

    cy.visit("/");
    cy.get('[data-cy="logIn-button"]').should("exist");

    cy.get('[data-cy="upload-step"]').should("exist");
    cy.get('[data-cy="validate-step"]').should("exist");
    cy.get('[data-cy="submit-step"]').should("exist");
    cy.get('[data-cy="done-step"]').should("exist");
    stepIsActive("upload");

    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload").its("response.statusCode").should("eq", 201);
    cy.wait("@validation").its("response.statusCode").should("eq", 200);
    stepIsCompleted("upload");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.get('[data-cy="logInForDelivery-button"]').should("exist");
  });

  it("correctly extracts error messages from the response", () => {
    cy.intercept({ url: "/api/v1/validation", method: "POST" }).as("upload");
    cy.intercept({ url: "/api/v1/validation", method: "GET" }).as("validation");

    let currentResponseIndex = 0;
    const responses = [
      { statusCode: 500, body: { detail: "Internal Server Error" } },
      { statusCode: 404, body: "Not found" },
    ];

    cy.intercept({ url: "/api/v1/delivery", method: "POST" }, req => {
      const currentResponse = responses[currentResponseIndex];
      req.reply({
        statusCode: currentResponse.statusCode,
        body: currentResponse.body,
      });

      currentResponseIndex = (currentResponseIndex + 1) % responses.length;
    }).as("deliveryRequest");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");

    setSelect("mandate", 1);

    cy.get('[data-cy="createDelivery-button"]').click();
    cy.wait("@deliveryRequest").its("response.statusCode").should("eq", 500);
    stepHasError("submit", true, "Internal Server Error");

    cy.get('[data-cy="createDelivery-button"]').click();
    cy.wait("@deliveryRequest").its("response.statusCode").should("eq", 404);
    stepHasError("submit", true, "Not found");
  });
});
