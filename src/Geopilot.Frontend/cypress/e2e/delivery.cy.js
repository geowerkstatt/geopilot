import { loadWithoutAuth, loginAsUploader } from "./helpers/appHelpers.js";
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
import {
  addFile,
  resetDelivery,
  stepHasError,
  stepIsActive,
  stepIsCompleted,
  stepIsLoading,
  uploadFile,
} from "./helpers/deliveryHelpers.js";

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
    // Limit the file types to a few extensions
    cy.intercept("/api/v1/validation", {
      statusCode: 200,
      body: { allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"] },
    }).as("fileExtensions");

    loadWithoutAuth();
    cy.dataCy("upload-step").should("exist");
    cy.dataCy("validate-step").should("exist");
    cy.dataCy("submit-step").should("not.exist");
    cy.dataCy("done-step").should("not.exist");
    stepIsActive("upload");

    cy.wait("@fileExtensions");
    cy.contains(".csv, .gpkg, .itf, .xml, .xtf or .zip (max. 100 MB)");

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
    cy.dataCy("validate-step").contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.dataCy("validate-step").contains("Die XML-Struktur der Transferdatei ist ungültig.");
    cy.dataCy("Log-button").should("not.exist");
    cy.dataCy("Xtf-Log-button").should("not.exist");
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
    cy.dataCy("validate-step").contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
    stepIsLoading("validate", false);
    stepHasError("validate", true, "Completed with errors");
    cy.dataCy("validate-step").contains("Die Daten sind nicht modellkonform.");
    cy.dataCy("Log-button").should("exist");
    cy.dataCy("Xtf-Log-button").should("exist");
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
    cy.dataCy("upload-step").should("exist");
    cy.dataCy("validate-step").should("exist");
    cy.dataCy("submit-step").should("exist");
    cy.dataCy("done-step").should("exist");
    stepIsActive("upload");

    // Selected file can be removed
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    stepHasError("upload", false);
    cy.dataCy("file-remove-button").click();
    cy.contains("ilimodels_not_conform").should("not.exist");
    stepIsActive("upload");

    // Can cancel the upload which will reset the form
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    resetDelivery("upload");
    cy.contains("ilimodels_not_conform.xml").should("not.exist");

    // Validation starts automatically after a file is uploaded
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.dataCy("upload-button").should("not.be.disabled");
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.dataCy("upload-step").contains("ilimodels_not_conform.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.dataCy("validate-step").contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.dataCy("upload-step").contains("ilimodels_not_conform.xml").should("not.exist");

    // Validation starts automatically after a file is uploaded
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.dataCy("upload-button").should("not.be.disabled");
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("upload", false);
    stepIsCompleted("upload");
    cy.dataCy("upload-step").contains("ilimodels_not_conform.xml");
    stepIsActive("validate");
    stepIsLoading("validate");
    cy.dataCy("validate-step").contains("The file is currently being validated with ilicheck...");

    // Validation can be cancelled
    resetDelivery("validate");
    cy.dataCy("upload-step").contains("ilimodels_not_conform.xml").should("not.exist");

    // Submit is active if validation is successful
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload");
    stepIsLoading("validate", true);
    cy.dataCy("validate-step").contains("The file is currently being validated with ilicheck...");
    cy.wait("@validation");
    stepIsLoading("validate", false);
    stepHasError("validate", false);
    cy.dataCy("validate-step").contains("Die Daten sind modellkonform.");
    cy.dataCy("Log-button").should("exist");
    cy.dataCy("Xtf-Log-button").should("exist");
    cy.dataCy("validate-step").dataCy("cancel-button").should("not.exist");
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
    cy.dataCy("upload-button").should("be.disabled");
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    cy.dataCy("upload-button").should("be.disabled");
    toggleCheckbox("acceptTermsOfUse");
    cy.dataCy("upload-button").should("not.be.disabled");
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

    cy.dataCy("createDelivery-button").should("be.disabled");
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
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Optional mandate does show optional input fields
    setSelect("mandate", 1, 3);
    cy.wait("@precursors");
    getFormField("precursor").should("exist");
    getFormField("isPartial").should("not.exist");
    getFormField("comment").should("exist");
    setSelect("precursor", 1);
    setInput("comment", "Temporary comment");
    setSelect("precursor", 0);
    setInput("comment", "");
    evaluateSelect("precursor", "");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Required mandate does show all input fields
    setSelect("mandate", 2, 3);
    cy.wait("@precursors");
    getFormField("precursor").should("exist");
    getFormField("isPartial").should("exist");
    getFormField("comment").should("exist");
    cy.dataCy("createDelivery-button").should("be.disabled");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);

    // Touch fields
    getFormInput("precursor").focus();
    getFormInput("comment").focus();
    cy.dataCy("createDelivery-button").focus();
    hasError("precursor", false);
    hasError("comment", false);

    // Set valid values
    setSelect("precursor", 1);
    toggleCheckbox("isPartial");
    setInput("comment", "This is a test comment.");
    hasError("precursor", false);
    hasError("isPartial", false);
    hasError("comment", false);
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Change of mandate clears inputs & errors
    setSelect("mandate", 0, 3);
    setSelect("mandate", 2, 3);
    getFormInput("precursor").should("not.contain.value");
    getFormInput("comment").should("not.contain.value");
    cy.dataCy("createDelivery-button").should("be.disabled");

    // Submit minimal delivery
    setSelect("mandate", 1, 3);
    cy.wait("@precursors");
    cy.dataCy("createDelivery-button").should("be.enabled");
    cy.dataCy("createDelivery-button").click();
    stepIsLoading("submit");

    cy.wait("@submit");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.dataCy("done-step").contains("The delivery was completed successfully.");

    // Can restart the delivery process after submitting was successful
    cy.dataCy("addAnotherDelivery-button").should("exist");
    cy.dataCy("addAnotherDelivery-button").click();
    stepIsActive("upload");
    stepIsCompleted("upload", false);
    stepIsActive("validate", false);
    stepIsCompleted("validate", false);
    stepIsActive("submit", false);
    stepIsCompleted("submit", false);
    stepIsActive("done", false);
    stepIsCompleted("done", false);
    cy.dataCy("upload-button").should("be.disabled");

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
    cy.dataCy("createDelivery-button").click();
    stepIsLoading("submit");
    cy.wait("@submit");
    stepIsCompleted("submit");
    stepIsActive("done");
    cy.dataCy("done-step").contains("The delivery was completed successfully.");
  });

  it("can log in during the delivery process", () => {
    mockValidationSuccess();

    cy.visit("/");
    cy.dataCy("logIn-button").should("exist");

    cy.dataCy("upload-step").should("exist");
    cy.dataCy("validate-step").should("exist");
    cy.dataCy("submit-step").should("exist");
    cy.dataCy("done-step").should("exist");
    stepIsActive("upload");

    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@upload").its("response.statusCode").should("eq", 201);
    cy.wait("@validation").its("response.statusCode").should("eq", 200);
    stepIsCompleted("upload");
    stepIsCompleted("validate");
    stepIsActive("submit");
    cy.dataCy("logInForDelivery-button").should("exist");
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

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v1/validation", method: "POST" },
      {
        statusCode: 418, // I'm a teapot
        body: {
          detail: "I'm a teapot",
        },
        delay: 500, // Added 500ms delay
      },
    ).as("customError");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    cy.wait("@customError");

    // Should display the actual error message since there's no mapping for 418
    stepHasError("upload", true, "I'm a teapot");
  });

  describe("Upload step errors", () => {
    it("displays file malformed error (400)", () => {
      cy.intercept(
        { url: "/api/v1/validation", method: "POST" },
        {
          statusCode: 400,
          body: {
            detail: "Bad Request",
          },
          delay: 500,
        },
      ).as("uploadError400");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@uploadError400");

      stepHasError("upload", true, "Invalid file format or corrupted file");
      cy.dataCy("upload-step").contains("Error 400");
    });

    it("displays file too large error (413)", () => {
      cy.intercept(
        { url: "/api/v1/validation", method: "POST" },
        {
          statusCode: 413,
          body: {
            detail: "Request Entity Too Large",
          },
          delay: 500,
        },
      ).as("uploadError413");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@uploadError413");

      stepHasError("upload", true, "Maximum file size exceeded");
      cy.dataCy("upload-step").contains("Error 413");
    });

    it("displays unexpected server error (500)", () => {
      cy.intercept(
        { url: "/api/v1/validation", method: "POST" },
        {
          statusCode: 500,
          body: {
            detail: "Internal Server Error",
          },
          delay: 500,
        },
      ).as("uploadError500");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@uploadError500");

      stepHasError("upload", true, "Unexpected server error during processing");
      cy.dataCy("upload-step").contains("Error 500");
    });
  });

  describe("Validation step errors", () => {
    beforeEach(() => {
      // Mock successful upload to reach validation step
      cy.intercept(
        { url: "/api/v1/validation", method: "POST" },
        {
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
        },
      ).as("upload");
    });

    it("displays request malformed error (400)", () => {
      cy.intercept("POST", "**/upload-endpoint", req => {
        // Delay the response by 1000ms (1 second)
        req.on("response", res => {
          res.setDelay(1000);
        });
      }).as("uploadRequest");

      cy.intercept(
        { url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" },
        {
          statusCode: 400,
          body: {
            detail: "Bad Request",
          },
          delay: 500,
        },
      ).as("validationError400");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validationError400");

      stepHasError("validate", true, "Invalid or corrupted request to the validation server");
      cy.dataCy("validate-step").contains("Error 400");
    });

    it("displays validation not found error (404)", () => {
      cy.intercept(
        { url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" },
        {
          statusCode: 404,
          body: {
            detail: "Not Found",
          },
          delay: 500,
        },
      ).as("validationError404");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validationError404");

      stepHasError("validate", true, "Validation process not found");
      cy.dataCy("validate-step").contains("Error 404");
    });
  });

  describe("Submit step errors", () => {
    beforeEach(() => {
      // Mock successful upload and validation to reach submit step
      cy.intercept(
        { url: "/api/v1/validation", method: "POST" },
        {
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
        },
      ).as("upload");

      cy.intercept(
        { url: "/api/v1/validation/d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" },
        {
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
        },
      ).as("validation");

      cy.intercept(
        { url: "/api/v1/mandate?jobId=d49ba857-5db5-45a0-b838-9d41cc7d8d64", method: "GET" },
        {
          statusCode: 200,
          body: [
            {
              id: 5,
              name: "Test Mandate",
              evaluatePrecursorDelivery: "optional",
              evaluatePartial: "notEvaluated",
              evaluateComment: "optional",
            },
          ],
          delay: 500,
        },
      ).as("mandates");

      cy.intercept({ url: "/api/v1/delivery?mandateId=*", method: "GET" }).as("precursors");
    });

    it("displays malformed request error (400)", () => {
      cy.intercept(
        { url: "/api/v1/delivery", method: "POST" },
        {
          statusCode: 400,
          body: {
            detail: "Bad Request",
          },
          delay: 500,
        },
      ).as("deliveryError400");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validation");
      cy.wait("@mandates");

      setSelect("mandate", 0);
      cy.wait("@precursors");

      cy.dataCy("createDelivery-button").click();
      cy.wait("@deliveryError400");

      stepHasError("submit", true, "Invalid or corrupted delivery request");
      cy.dataCy("submit-step").contains("Error 400");
    });

    it("displays unauthorized error (401)", () => {
      cy.intercept(
        { url: "/api/v1/delivery", method: "POST" },
        {
          statusCode: 401,
          body: {
            detail: "Unauthorized",
          },
          delay: 500,
        },
      ).as("deliveryError401");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validation");
      cy.wait("@mandates");

      setSelect("mandate", 0);
      cy.wait("@precursors");

      cy.dataCy("createDelivery-button").click();
      cy.wait("@deliveryError401");

      stepHasError("submit", true, "User is not authorized to make deliveries");
      cy.dataCy("submit-step").contains("Error 401");
    });

    it("displays validation not found error (404)", () => {
      cy.intercept(
        { url: "/api/v1/delivery", method: "POST" },
        {
          statusCode: 404,
          body: {
            detail: "Not Found",
          },
          delay: 500,
        },
      ).as("deliveryError404");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validation");
      cy.wait("@mandates");

      setSelect("mandate", 0);
      cy.wait("@precursors");

      cy.dataCy("createDelivery-button").click();
      cy.wait("@deliveryError404");

      stepHasError("submit", true, "Validation results could not be found");
      cy.dataCy("submit-step").contains("Error 404");
    });

    it("displays unexpected error (500)", () => {
      cy.intercept(
        { url: "/api/v1/delivery", method: "POST" },
        {
          statusCode: 500,
          body: {
            detail: "Internal Server Error",
          },
          delay: 500,
        },
      ).as("deliveryError500");

      loginAsUploader();
      addFile("deliveryFiles/ilimodels_valid.xml", true);
      uploadFile();
      cy.wait("@upload");
      cy.wait("@validation");
      cy.wait("@mandates");

      setSelect("mandate", 0);
      cy.wait("@precursors");

      cy.dataCy("createDelivery-button").click();
      cy.wait("@deliveryError500");

      stepHasError("submit", true, "An unexpected error occurred during delivery");
      cy.dataCy("submit-step").contains("Error 500");
    });
  });
});
