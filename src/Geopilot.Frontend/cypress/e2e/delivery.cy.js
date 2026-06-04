import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers.js";
import { hasError, setSelect } from "./helpers/formHelpers.js";
import {
  addFile,
  stepHasError,
  stepIsActive,
  stepIsLoading,
  uploadFile,
  selectMandate,
  startProcessing,
  selectStep,
} from "./helpers/deliveryHelpers.js";

describe("Delivery tests", () => {
  it("can only upload one supported file", () => {
    // Limit the file types to a few extensions
    cy.intercept("GET", "/api/v2/processing", {
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

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_valid.xtf"], false);
    stepHasError("upload", true, "The maximum number of files has been exceeded");

    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepHasError("upload", false);
    uploadFile();

    stepIsActive("selectMandate");
  });

  it("shows processing error without log files", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    selectMandate(1);
    startProcessing();
    stepIsLoading("process", true);
    stepHasError("process", true, "Failed");
    cy.dataCy("processing-step-validation").dataCy("processing-step-icon-error").should("exist");
    cy.dataCy("errorLog.log-button").should("not.exist");
    cy.dataCy("xtfLog.xtf-button").should("not.exist");
    stepIsActive("process");
    stepIsActive("submit", false); // Should not be active if processing has errors
    cy.dataCy("continue-button").should("not.exist");
  });

  it("can submit delivery", () => {
    cy.intercept("/api/v1/delivery?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepIsActive("upload");
    uploadFile();

    stepIsActive("selectMandate");
    selectMandate(1);
    startProcessing();

    stepIsActive("process");

    // XTF log files should be available
    cy.dataCy("errorLog.log-button").should("exist");
    cy.dataCy("xtfLog.xtf-button").should("exist");

    cy.dataCy("continue-button").click();
    stepIsActive("submit");

    //Wait for select values to be present on DOM
    cy.wait("@precursors");
    cy.wait(200);

    // Declare delivery metadata
    setSelect("precursor", 0);
    hasError("precursor", false);
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Complete delivery
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    stepIsActive("submit", false);
    stepIsActive("done");
  });

  it("displays error if no mandates were found", () => {
    loginAsNewUser();
    addFile("deliveryFiles/ilimodels_invalid.xml", true);
    uploadFile();
    stepIsActive("selectMandate");
    stepHasError("selectMandate", true, "No suitable mandate was found for your delivery");
  });

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v2/processing", method: "POST" },
      {
        statusCode: 418, // I'm a teapot
        body: {
          detail: "I'm a teapot",
        },
        delay: 500, // Added 500ms delay
      },
    ).as("customError");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();
    cy.wait("@customError");

    // Should display the actual error message since there's no mapping for 418
    stepHasError("upload", true, "I'm a teapot");
  });

  it("can show previous steps as read-only", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();

    selectMandate(1);
    startProcessing();
    stepIsActive("process");

    // Can navigate with back button
    cy.dataCy("back-button").click();
    stepIsActive("selectMandate");
    stepIsActive("process", false);

    // Can navigate by clicking on the step
    selectStep("upload");
    stepIsActive("upload");
    cy.dataCy("upload-button").should("not.exist");

    cy.dataCy("continue-button").click();
    // Select mandate step shows previously selected mandate
    stepIsActive("selectMandate");
    cy.dataCy("mandate-1").should("have.class", "Mui-selected").should("have.class", "Mui-disabled");

    // Can not navigate to future steps
    selectStep("submit");
    stepIsActive("submit", false);

    selectStep("process");
    stepIsActive("process");
  });
});
