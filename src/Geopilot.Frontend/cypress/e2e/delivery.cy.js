import { loadWithoutAuth, loginAsNewUser, loginAsUploader } from "./helpers/appHelpers.js";
import { hasError, setSelect } from "./helpers/formHelpers.js";
import {
  addFile,
  stepHasError,
  stepIsActive,
  stepIsCompleted,
  stepIsLoading,
  uploadFile,
  selectMandate,
  startProcessing,
  selectStep,
} from "./helpers/deliveryHelpers.js";

describe("Delivery tests", () => {
  it("can only upload supported file types", () => {
    // Limit the file types to a few extensions
    cy.intercept("GET", "/api/v2/processing", {
      statusCode: 200,
      body: { allowedFileExtensions: [".csv", ".gpkg", ".itf", ".xml", ".xtf", ".zip"] },
    }).as("fileExtensions");

    loadWithoutAuth();
    cy.dataCy("files-step").should("exist");
    cy.dataCy("mandate-step").should("exist");
    cy.dataCy("processing-step").should("exist");
    cy.dataCy("delivery-step").should("not.exist");
    stepIsActive("files", true);

    cy.wait("@fileExtensions");

    addFile("deliveryFiles/picture-type.png", false);
    stepHasError("files", true, "The file type is not supported");

    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepHasError("files", false);

    addFile(["deliveryFiles/ilimodels_invalid.xml", "deliveryFiles/ilimodels_not_conform.xml"], true);
    stepHasError("files", false);

    cy.dataCy("file-list-item").should("have.length", 3);

    uploadFile();

    stepIsActive("mandate");
  });

  // Skip test as starting the processing currently results in a 500 when running in the github action
  it.skip("shows processing error without log files", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_not_conform.xml", true);
    uploadFile();
    selectMandate(1);
    startProcessing();
    stepIsLoading("processing", true);
    stepHasError("processing", true, "Failed");
    cy.dataCy("processing-step-validation").dataCy("processing-step-icon-error").should("exist");
    cy.dataCy("errorLog.log-button").should("not.exist");
    cy.dataCy("xtfLog.xtf-button").should("not.exist");
    stepIsActive("processing");
    stepIsActive("delivery", false); // Should not be active if processing has errors
    cy.dataCy("continue-button").should("be.disabled");
  });

  // Skip test as starting the processing currently results in a 500 when running in the github action
  it.skip("can submit delivery", () => {
    cy.intercept("/api/v1/delivery?mandateId=*").as("precursors");

    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    stepIsActive("files");
    uploadFile();

    stepIsActive("mandate");
    selectMandate(1);
    startProcessing();

    stepIsActive("processing");

    // XTF log files should be available
    cy.dataCy("errorLog.log-button").should("exist");
    cy.dataCy("xtfLog.xtf-button").should("exist");

    cy.dataCy("continue-button").click();
    stepIsActive("delivery");

    //Wait for select values to be present on DOM
    cy.wait("@precursors");
    cy.wait(200);

    // Declare delivery metadata
    setSelect("precursor", 0);
    hasError("precursor", false);
    cy.dataCy("createDelivery-button").should("be.enabled");

    // Complete delivery
    cy.dataCy("createDelivery-button").should("be.enabled").click();
    stepIsActive("delivery");
    stepIsCompleted("delivery");
  });

  it("displays error if no mandates were found", () => {
    loginAsNewUser();
    addFile("deliveryFiles/ilimodels_invalid.xml", true);
    uploadFile();
    stepIsActive("mandate");
    stepHasError("mandate", true, "No suitable mandate was found for your delivery");
  });

  it("displays custom error messages when they don't match predefined errors", () => {
    cy.intercept(
      { url: "/api/v2/upload", method: "POST" },
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
    stepHasError("files", true, "I'm a teapot");
  });

  it("can show previous steps as read-only", () => {
    loginAsUploader();
    addFile("deliveryFiles/ilimodels_valid.xtf", true);
    uploadFile();

    cy.intercept("GET", "/api/v1/mandate?uploadId=*").as("getMandates");
    cy.wait("@getMandates");

    selectMandate(1);
    startProcessing();
    stepIsActive("processing");

    // Can navigate with back button
    cy.dataCy("back-button").click();
    stepIsActive("mandate");
    stepIsActive("processing", false);

    // Can navigate by clicking on the step
    selectStep("files");
    stepIsActive("files");
    cy.dataCy("upload-button").should("not.exist");

    cy.dataCy("continue-button").click();
    // Select mandate step shows previously selected mandate
    stepIsActive("mandate");
    cy.dataCy("mandate-1").should("have.class", "Mui-selected").should("have.class", "Mui-disabled");

    // Can not navigate to future steps
    selectStep("delivery");
    stepIsActive("delivery", false);

    selectStep("processing");
    stepIsActive("processing");
  });

  it("renders content carousel on mobile with only the active step mounted", () => {
    cy.viewport("iphone-x");
    loadWithoutAuth();
    stepIsActive("files", true);

    cy.dataCy("delivery-content-carousel").should("exist");
    cy.dataCy("file-dropzone").should("exist");
    cy.dataCy("mandate-selection-group").should("not.exist");
  });
});
