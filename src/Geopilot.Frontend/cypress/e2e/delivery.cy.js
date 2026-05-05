import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers.js";
import { hasError, setInput, setSelect } from "./helpers/formHelpers.js";
import {
  addFile,
  stepHasError,
  stepIsActive,
  stepIsLoading,
  uploadFile,
  selectMandate,
  startProcessing,
} from "./helpers/deliveryHelpers.js";

describe("Delivery tests", () => {
  it("shows only processing steps if auth settings could not be loaded", () => {
    // Limit the file types to a few extensions
    cy.intercept("GET", "/api/v1/processing", {
      statusCode: 200,
      body: { allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"] },
    }).as("fileExtensions");

    loadWithoutAuth();
    cy.dataCy("upload-step").should("exist");
    cy.dataCy("process-step").should("exist");
    cy.dataCy("submit-step").should("not.exist");
    cy.dataCy("done-step").should("exist");
    stepIsActive("upload", true);

    cy.wait("@fileExtensions");
    cy.contains(".csv, .gpkg, .itf, .xml, .xtf or .zip (max. 100 MB)");

    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("upload", true, "The file type is not supported");

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_valid.xml"], false);
    stepHasError("upload", true, "Only one file can be checked at a time");

    addFile("deliveryFiles/ilimodels_valid.xml", true);
    stepHasError("upload", false);
    uploadFile();

    stepIsActive("process");
    cy.dataCy("createDelivery-button").should("not.exist");
    cy.dataCy("validateOnly-button").should("be.visible").should("not.be.disabled").click();
    stepIsLoading("process", true);
    stepIsActive("done");
  });

  it("shows processing error without log files", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    selectMandate(0, 5);
    startProcessing();
    stepIsLoading("process", true);
    cy.dataCy("process-step").contains("The file is being processed with XTF Validation...");
    stepHasError("process", true, "Completed with errors");
    cy.dataCy("process-step").contains("XTF Validation");
    cy.dataCy("error_log-button").should("not.exist");
    cy.dataCy("xtf_log-button").should("not.exist");
    stepIsActive("process");
    stepIsActive("submit", false); // Should not be active if processing has errors
  });

  it("can submit delivery", () => {
    cy.intercept("/api/v1/delivery?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    stepIsActive("upload");
    selectMandate(0, 5);
    startProcessing();
    stepIsActive("process");
    stepIsActive("submit");

    // XTF log files should be available
    cy.dataCy("error_log-button").should("exist");
    cy.dataCy("xtf_log-button").should("exist");

    //Wait for select values to be present on DOM
    cy.wait("@precursors");
    cy.wait(200);

    // Declare delivery metadata
    setSelect("precursor", 0);
    hasError("precursor", false);
    hasError("comment", false);
    cy.dataCy("createDelivery-button").should("be.enabled");
    setInput("comment", "Temporary comment");

    // Complete delivery
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    stepIsActive("submit", false);
    stepIsActive("done");
  });

  it("can log in during the delivery process", () => {
    cy.visit("/");
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();

    // Check only existence of button as popup is not visible in Cypress
    cy.dataCy("logInForDelivery-button").should("exist").click();
  });

  it.only("displays error if no mandates were found", () => {
    loginAsNewUser();
    addFile("deliveryFiles/ilimodels_valid.xml", true);
    uploadFile();
    stepIsActive("process");
    stepHasError("process", true, "No suitable mandate was found for your delivery");
  });

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v1/processing", method: "POST" },
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
});
