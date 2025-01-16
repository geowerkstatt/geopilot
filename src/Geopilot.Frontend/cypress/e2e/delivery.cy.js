import { loadWithoutAuth, loginAsUploader } from "./helpers/appHelpers.js";
import { clickCancel } from "./helpers/buttonHelpers.js";
import {
  evaluateSelect,
  getFormField,
  getFormInput,
  hasError,
  isDisabled,
  setInput,
  setSelect,
  toggleCheckbox,
} from "./helpers/formHelpers.js";

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

const mockMandates = () => {
  cy.intercept({ url: "/api/v1/mandate?jobId=d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" }, req => {
    req.reply({
      statusCode: 200,
      body: [
        {
          id: 1,
          name: "Handmade Soft Cheese",
          evaluatePrecursorDelivery: "notEvaluated",
          evaluatePartial: "notEvaluated",
          evaluateComment: "notEvaluated",
        },
        {
          id: 5,
          name: "Licensed Frozen Towels",
          evaluatePrecursorDelivery: "optional",
          evaluatePartial: "notEvaluated",
          evaluateComment: "optional",
        },
        {
          id: 9,
          name: "Unbranded Wooden Pants",
          evaluatePrecursorDelivery: "required",
          evaluatePartial: "required",
          evaluateComment: "required",
        },
      ],
      delay: 500,
    });
  }).as("mandates");
};

describe("Delivery tests", () => {
  beforeEach(() => {
    mockUploadSuccess();
  });

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

    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("upload", true, "The file type is not supported");

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_valid.xml"], false);
    stepHasError("upload", true, "Only one file can be checked at a time");

    // Cypress doesn't support attaching large files, so we cannot test the file size validation here.
  });

  it("shows validation error: invalid structure", () => {
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

  it("can cancel at any step", () => {
    mockValidationSuccess();
    mockMandates();
    mockUploadSuccess();
    cy.intercept({ url: "/api/v1/delivery?mandateId=*", method: "GET" }).as("precursors");
    cy.intercept({ url: "/api/v1/delivery", method: "POST" }, req => {
      req.reply({
        statusCode: 201,
        body: {
          id: 43,
          jobId: "d49ba857-5db5-45a0-b838-9d41cc7d8d64",
        },
        delay: 500,
      });
    }).as("submit");

    loginAsUploader();

    // All steps are visible
    cy.get('[data-cy="upload-step"]').should("exist");
    cy.get('[data-cy="validate-step"]').should("exist");
    cy.get('[data-cy="submit-step"]').should("exist");
    cy.get('[data-cy="done-step"]').should("exist");
    stepIsActive("upload");

    // All file types are allowed
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    stepHasError("upload", false);

    // Selected file can be removed
    cy.get('[data-cy="file-remove-button"]').click();
    cy.contains("invalid-type.png").should("not.exist");
    stepIsActive("upload");

    // Can cancel the upload which will reset the form
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    resetDelivery("upload");
    cy.contains("ilimodels_not_conform.xml").should("not.exist");

    // Validation starts automatically after a file is uploaded
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml").should("not.exist");

    // Validation starts automatically after a file is uploaded
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.get('[data-cy="validate-step"]').contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.get('[data-cy="upload-step"]').contains("ilimodels_not_conform.xml").should("not.exist");

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
  });

  it("can upload any filetype", () => {
    mockValidationSuccess();

    loginAsUploader();
    stepIsActive("upload");

    // All file types are allowed
    addFile("deliveryFiles/picture-type.png", true);
    stepHasError("upload", false);
    uploadFile();

    stepIsCompleted("upload");
  });

  it("checks if terms of use are accepted", () => {
    loginAsUploader();
    mockUploadSuccess();
    addFile("deliveryFiles/ilimodels_valid.xml", true);

    // Can upload a file once the terms of use are accepted and the file is valid
    cy.get('[data-cy="upload-button"]').should("be.disabled");
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.get('[data-cy="upload-button"]').should("be.disabled");
    toggleCheckbox("acceptTermsOfUse");
    cy.get('[data-cy="upload-button"]').should("not.be.disabled");
    uploadFile();

    cy.wait("@upload");
    stepIsCompleted("upload");
  });

  it("can submit delivery", () => {
    mockValidationSuccess();
    mockMandates();
    cy.intercept({ url: "/api/v1/delivery?mandateId=*", method: "GET" }).as("precursors");
    cy.intercept({ url: "/api/v1/delivery", method: "POST" }, req => {
      req.reply({
        statusCode: 201,
        body: {
          id: 43,
          jobId: "d49ba857-5db5-45a0-b838-9d41cc7d8d64",
        },
        delay: 500,
      });
    }).as("submit");

    loginAsUploader();

    // Add delivery with minimal form
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.wait("@mandates");
    cy.wait(500); // Wait for the select to be populated and enabled

    cy.get('[data-cy="createDelivery-button"]').should("be.disabled");
    isDisabled("mandate", false);

    getFormField("precursor").should("not.exist");
    getFormField("isPartial").should("not.exist");
    getFormField("comment").should("not.exist");

    // NotEvaluated mandate does not show input fields;
    setSelect("mandate", 0, 3);
    cy.wait("@precursors");
    getFormField("precursor").should("not.exist");
    getFormField("isPartial").should("not.exist");
    getFormField("comment").should("not.exist");
    cy.get('[data-cy="createDelivery-button"]').should("be.enabled");

    // Optional mandate does show optional input fields
    setSelect("mandate", 1, 3);
    cy.wait("@precursors");
    getFormField("precursor").should("exist");
    getFormField("isPartial").should("not.exist");
    getFormField("comment").should("exist");
    setSelect("precursor", 0); // reset
    setInput("comment", "");
    evaluateSelect("precursor", "");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);
    cy.get('[data-cy="createDelivery-button"]').should("be.enabled");

    // Required mandate does show all input fields
    setSelect("mandate", 2, 3);
    cy.wait("@precursors");
    getFormField("precursor").should("exist");
    getFormField("isPartial").should("exist");
    getFormField("comment").should("exist");
    cy.get('[data-cy="createDelivery-button"]').should("be.disabled");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);

    // Touch fields
    getFormInput("precursor").focus();
    getFormInput("comment").focus();
    cy.get('[data-cy="createDelivery-button"]').focus();
    hasError("precursor", false);
    hasError("comment", false);

    // Set valid values
    setSelect("precursor", 1);
    toggleCheckbox("isPartial");
    setInput("comment", "This is a test comment.");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);
    cy.get('[data-cy="createDelivery-button"]').should("be.enabled");

    // Change of mandate clears inputs & errors
    setSelect("mandate", 0, 3);
    setSelect("mandate", 2, 3);
    getFormInput("precursor").should("not.contain.value");
    getFormInput("comment").should("not.contain.value");
    cy.get('[data-cy="createDelivery-button"]').should("be.disabled");

    // Submit minimal delivery
    setSelect("mandate", 1, 3);
    cy.wait("@precursors");
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

    // Add delivery with previously completed form
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.wait("@mandates");
    cy.wait(1000); // Wait for the select to be populated and enabled

    setSelect("mandate", 1);
    cy.wait("@precursors");
    cy.get('[data-cy="createDelivery-button"]').click();
    stepIsLoading("submit");
    cy.wait("@submit");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.get('[data-cy="done-step"]').contains("The delivery was completed successfully.");
  });

  it("can log in during the delivery process", () => {
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
    mockValidationSuccess();
    mockMandates();

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
    cy.wait("@mandates");

    setSelect("mandate", 1);

    cy.get('[data-cy="createDelivery-button"]').click();
    cy.wait("@deliveryRequest").its("response.statusCode").should("eq", 500);
    stepHasError("submit", true, "Internal Server Error");

    cy.get('[data-cy="createDelivery-button"]').click();
    cy.wait("@deliveryRequest").its("response.statusCode").should("eq", 404);
    stepHasError("submit", true, "Not found");
  });

  it("displays error if no mandates were found", () => {
    mockValidationSuccess();
    cy.intercept({ url: "/api/v1/mandate?jobId=d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" }, req => {
      req.reply({
        statusCode: 200,
        body: [],
        delay: 500,
      });
    }).as("mandates");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    cy.wait("@validation");
    cy.wait("@mandates");

    stepHasError("submit", true, "No suitable mandate was found for your delivery");
    isDisabled("mandate", true);
    resetDelivery("submit");
  });
});
